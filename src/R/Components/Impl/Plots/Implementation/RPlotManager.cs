﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Common.Core;
using Microsoft.Common.Core.Disposables;
using Microsoft.Common.Core.IO;
using Microsoft.Common.Core.Shell;
using Microsoft.R.Components.Extensions;
using Microsoft.R.Components.InteractiveWorkflow;
using Microsoft.R.Components.Settings;
using Microsoft.R.Host.Client;
using Microsoft.R.Host.Client.Session;

namespace Microsoft.R.Components.Plots.Implementation {
    internal class RPlotManager : IRPlotManager {
        private readonly IRInteractiveWorkflow _interactiveWorkflow;
        private readonly IFileSystem _fileSystem;
        private readonly DisposableBag _disposableBag;
        private readonly List<IRPlotDevice> _devices = new List<IRPlotDevice>();
        private readonly Dictionary<int, IRPlotDeviceVisualComponent> _visualComponents = new Dictionary<int, IRPlotDeviceVisualComponent>();
        private readonly Dictionary<Guid, IRPlotDeviceVisualComponent> _assignedVisualComponents = new Dictionary<Guid, IRPlotDeviceVisualComponent>();
        private readonly List<IRPlotDeviceVisualComponent> _unassignedVisualComponents = new List<IRPlotDeviceVisualComponent>();
        private readonly ICoreShell _shell;

        public event EventHandler<RPlotDeviceEventArgs> ActiveDeviceChanged;
        public event EventHandler<RPlotDeviceEventArgs> DeviceAdded;
        public event EventHandler<RPlotDeviceEventArgs> DeviceRemoved;

        public RPlotManager(IRSettings settings, IRInteractiveWorkflow interactiveWorkflow, IFileSystem fileSystem) {
            _interactiveWorkflow = interactiveWorkflow;
            _fileSystem = fileSystem;
            _shell = _interactiveWorkflow.Shell;

            _disposableBag = DisposableBag.Create<RPlotManager>()
                .Add(() => interactiveWorkflow.RSession.Connected -= RSession_Connected)
                .Add(() => interactiveWorkflow.RSession.Mutated -= RSession_Mutated);

            interactiveWorkflow.RSession.Connected += RSession_Connected;
            interactiveWorkflow.RSession.Mutated += RSession_Mutated;
        }


        public IRInteractiveWorkflow InteractiveWorkflow => _interactiveWorkflow;

        public IRPlotHistoryVisualComponent HistoryVisualComponent { get; private set; }

        public IRPlotDevice ActiveDevice { get; private set; }

        public void Dispose() {
            _disposableBag.TryDispose();
        }

        public IRPlotDeviceVisualComponent GetOrCreateVisualComponent(IRPlotDeviceVisualComponentContainerFactory visualComponentContainerFactory, int instanceId) {
            _shell.AssertIsOnMainThread();

            IRPlotDeviceVisualComponent component;
            if (_visualComponents.TryGetValue(instanceId, out component)) {
                return component;
            }

            component = visualComponentContainerFactory.GetOrCreate(this, _interactiveWorkflow.RSession, instanceId).Component;
            _disposableBag.Add(component);
            _visualComponents[instanceId] = component;
            return component;
        }

        public IRPlotHistoryVisualComponent GetOrCreateVisualComponent(IRPlotHistoryVisualComponentContainerFactory visualComponentContainerFactory, int instanceId) {
            _shell.AssertIsOnMainThread();

            return HistoryVisualComponent ?? (HistoryVisualComponent = visualComponentContainerFactory.GetOrCreate(this, instanceId).Component);
        }

        public IRPlotDeviceVisualComponent GetPlotVisualComponent(IRPlotDevice device) {
            _shell.AssertIsOnMainThread();

            IRPlotDeviceVisualComponent visualComponent = null;
            _assignedVisualComponents.TryGetValue(device.DeviceId, out visualComponent);
            return visualComponent;
        }

        public IRPlotDeviceVisualComponent GetPlotVisualComponent(int instanceId) {
            _shell.AssertIsOnMainThread();

            IRPlotDeviceVisualComponent visualComponent = null;
            _visualComponents.TryGetValue(instanceId, out visualComponent);
            return visualComponent;
        }

        public void RegisterVisualComponent(IRPlotDeviceVisualComponent visualComponent) {
            _shell.AssertIsOnMainThread();

            _unassignedVisualComponents.Add(visualComponent);
        }

        public async Task DeviceDestroyedAsync(Guid deviceId, CancellationToken cancellationToken) {
            await _shell.SwitchToMainThreadAsync(cancellationToken);

            var device = FindDevice(deviceId);
            Debug.Assert(device != null, "List of devices is out of sync.");
            if (device == null) {
                return;
            }

            _devices.Remove(device);

            IRPlotDeviceVisualComponent component;
            if (_assignedVisualComponents.TryGetValue(deviceId, out component)) {
                component.Unassign();
                _assignedVisualComponents.Remove(deviceId);
                _unassignedVisualComponents.Add(component);
            } else {
                Debug.Assert(false, "Failed to destroy a plot visual component.");
            }

            DeviceRemoved?.Invoke(this, new RPlotDeviceEventArgs(device));
        }

        public async Task LoadPlotAsync(PlotMessage plot, CancellationToken cancellationToken) {
            await _shell.SwitchToMainThreadAsync(cancellationToken);

            var device = FindDevice(plot.DeviceId);
            device.DeviceNum = plot.DeviceNum;

            if (plot.IsClearAll) {
                device.Clear();
            } else if (plot.IsPlot) {
                try {
                    var img = plot.ToBitmapImage();

                    // Remember the size of the last plot.
                    // We'll use that when exporting the plot to image/pdf.
                    device.PixelWidth = img.PixelWidth;
                    device.PixelHeight = img.PixelHeight;
                    device.Resolution = (int)Math.Round(img.DpiX);

                    device.AddOrUpdate(plot.PlotId, img);
                } catch (Exception e) when (!e.IsCriticalException()) {
                }
            } else if (plot.IsError) {
                device.AddOrUpdate(plot.PlotId, null);
            }

            var visualComponent = GetVisualComponentForDevice(plot.DeviceId);
            if (visualComponent != null) {
                visualComponent.Container.Show(focus: false, immediate: false);
                visualComponent.Container.UpdateCommandStatus(false);
            }
        }

        public async Task<PlotDeviceProperties> DeviceCreatedAsync(Guid deviceId, CancellationToken cancellationToken) {
            await _shell.SwitchToMainThreadAsync(cancellationToken);

            var device = new RPlotDevice(deviceId);
            _devices.Add(device);

            PlotDeviceProperties props;

            var visualComponent = GetVisualComponentForDevice(deviceId);
            if (visualComponent != null) {
                visualComponent.Container.Show(focus: false, immediate: true);
                props = visualComponent.GetDeviceProperties();
            } else {
                Debug.Assert(false, "Failed to create a plot visual component.");
                props = PlotDeviceProperties.Default;
            }

            device.PixelWidth = props.Width;
            device.PixelHeight = props.Height;
            device.Resolution = props.Resolution;

            DeviceAdded?.Invoke(this, new RPlotDeviceEventArgs(device));

            return props;
        }

        public async Task<LocatorResult> StartLocatorModeAsync(Guid deviceId, CancellationToken cancellationToken) {
            await _shell.SwitchToMainThreadAsync(cancellationToken);

            var visualComponent = GetVisualComponentForDevice(deviceId);
            if (visualComponent == null) {
                return default(LocatorResult);
            }

            visualComponent.Container.Show(focus: false, immediate: true);
            return await visualComponent.StartLocatorModeAsync(cancellationToken);
        }

        public async Task RemoveAllPlotsAsync(IRPlotDevice device) {
            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await _interactiveWorkflow.RSession.ClearPlotHistoryAsync(device.DeviceId);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public async Task RemovePlotAsync(IRPlot plot) {
            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await _interactiveWorkflow.RSession.RemoveCurrentPlotAsync(plot.ParentDevice.DeviceId, plot.PlotId);

                plot.ParentDevice.Remove(plot);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public async Task ActivatePlotAsync(IRPlot plot) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            if (HistoryVisualComponent != null) {
                if (HistoryVisualComponent.AutoHide) {
                    HistoryVisualComponent.Container.Hide();
                }
            }

            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await _interactiveWorkflow.RSession.SelectPlotAsync(plot.ParentDevice.DeviceId, plot.PlotId);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public async Task NextPlotAsync(IRPlotDevice device) {
            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await _interactiveWorkflow.RSession.NextPlotAsync(device.DeviceId);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public async Task PreviousPlotAsync(IRPlotDevice device) {
            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await _interactiveWorkflow.RSession.PreviousPlotAsync(device.DeviceId);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public async Task ResizeAsync(IRPlotDevice device, int pixelWidth, int pixelHeight, int resolution) {
            if (!_interactiveWorkflow.RSession.IsHostRunning) {
                return;
            }

            try {
                await _interactiveWorkflow.RSession.ResizePlotAsync(device.DeviceId, pixelWidth, pixelHeight, resolution);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public Task ExportToBitmapAsync(IRPlot plot, string deviceName, string outputFilePath, int pixelWidth, int pixelHeight, int resolution) =>
            ExportAsync(outputFilePath, _interactiveWorkflow.RSession.ExportPlotToBitmapAsync(plot.ParentDevice.DeviceId, plot.PlotId, deviceName, outputFilePath, pixelWidth, pixelHeight, resolution));

        public Task ExportToMetafileAsync(IRPlot plot, string outputFilePath, double inchWidth, double inchHeight, int resolution) =>
            ExportAsync(outputFilePath, _interactiveWorkflow.RSession.ExportPlotToMetafileAsync(plot.ParentDevice.DeviceId, plot.PlotId, outputFilePath, inchWidth, inchHeight, resolution));

        public Task ExportToPdfAsync(IRPlot plot, string outputFilePath, double inchWidth, double inchHeight) =>
            ExportAsync(outputFilePath, _interactiveWorkflow.RSession.ExportToPdfAsync(plot.ParentDevice.DeviceId, plot.PlotId, outputFilePath, inchWidth, inchHeight));

        public async Task ActivateDeviceAsync(IRPlotDevice device) {
            Debug.Assert(device != null);
            await _interactiveWorkflow.RSession.ActivatePlotDeviceAsync(device.DeviceId);
        }

        public async Task NewDeviceAsync(int existingInstanceId) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            if (existingInstanceId >= 0) {
                // User wants to create a graphics device for an existing unassigned visual component.
                // Before asking the host to create a graphics device, we adjust the unassigned
                // visual component list so the desired visual component is used when the next device
                // is created.
                SetNextVisualComponent(existingInstanceId);
            }

            // Force creation of the graphics device
            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await _interactiveWorkflow.RSession.NewPlotDeviceAsync();
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        public async Task CopyOrMovePlotFromAsync(Guid sourceDeviceId, Guid sourcePlotId, IRPlotDevice targetDevice, bool isMove) {
            Debug.Assert(targetDevice != null);

            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            var sourcePlot = FindPlot(sourceDeviceId, sourcePlotId);
            if (sourcePlot != null) {
                await CopyPlotAsync(sourcePlot, targetDevice);
                if (isMove) {
                    await RemovePlotAsync(sourcePlot);

                    // Removing that plot may activate the device from the removed plot,
                    // which would hide this device. So we re-show it.
                    ShowDevice(targetDevice);
                }
            }
        }

        public IRPlot[] GetAllPlots() {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            var plots = new List<IRPlot>();
            foreach (var device in _devices) {
                for (int i = 0; i < device.PlotCount; i++) {
                    plots.Add(device.GetPlotAt(i));
                }
            }

            return plots.ToArray();
        }

        private async Task CopyPlotAsync(IRPlot sourcePlot, IRPlotDevice targetDevice) {
            await TaskUtilities.SwitchToBackgroundThread();
            try {
                await InteractiveWorkflow.RSession.CopyPlotAsync(sourcePlot.ParentDevice.DeviceId, sourcePlot.PlotId, targetDevice.DeviceId);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        private void ShowDevice(IRPlotDevice device) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            var visualComponent = GetVisualComponentForDevice(device.DeviceId);
            visualComponent?.Container.Show(focus: false, immediate: false);
        }

        private async Task ExportAsync(string outputFilePath, Task<ulong> exportTask) {
            try {
                var result = await exportTask;
                using(DataTransferSession dts = new DataTransferSession(InteractiveWorkflow.RSession, _fileSystem)) {
                    await dts.FetchFileAsync(new RBlobInfo(result), outputFilePath);
                }
            } catch (IOException ex) {
                throw new RPlotManagerException(ex.Message, ex);
            } catch (RException ex) {
                throw new RPlotManagerException(string.Format(CultureInfo.InvariantCulture, Resources.Plots_EvalError, ex.Message), ex);
            }
        }

        private IRPlotDeviceVisualComponent GetVisualComponentForDevice(Guid deviceId) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            // If we've already assigned a plot window, reuse it
            IRPlotDeviceVisualComponent component = GetPlotDeviceVisualComponent(deviceId);
            if (component != null) {
                return component;
            }

            // Recycle one that doesn't have a plot device assigned, or create a new one if none available
            component = GetOrCreatePlotDeviceVisualComponent();

            var device = FindDevice(deviceId);
            component.Assign(device);

            _assignedVisualComponents[deviceId] = component;

            if (_unassignedVisualComponents.Contains(component)) {
                _unassignedVisualComponents.Remove(component);
            }

            return component;
        }

        private IRPlotDeviceVisualComponent GetPlotDeviceVisualComponent(Guid deviceId) {
            IRPlotDeviceVisualComponent component;

            if (_assignedVisualComponents.TryGetValue(deviceId, out component)) {
                return component;
            }

            return null;
        }

        private IRPlotDeviceVisualComponent GetOrCreatePlotDeviceVisualComponent() {
            IRPlotDeviceVisualComponent component = null;

            // If we have an unused plot window, reuse it
            if (_unassignedVisualComponents.Count > 0) {
                component = _unassignedVisualComponents[0];
            }

            // If we have no plot window to reuse, create one
            if (component == null) {
                var containerFactory = InteractiveWorkflow.Shell.ExportProvider.GetExportedValue<IRPlotDeviceVisualComponentContainerFactory>();
                component = GetOrCreateVisualComponent(containerFactory, GetUnusedInstanceId());
            }

            return component;
        }

        public IRPlotDeviceVisualComponent GetOrCreateMainPlotVisualComponent() {
            return ActiveDevice != null ? GetPlotDeviceVisualComponent(ActiveDevice.DeviceId) : GetOrCreatePlotDeviceVisualComponent();
        }

        private IRPlotDevice FindDevice(Guid deviceId) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            return _devices.SingleOrDefault(d => d.DeviceId == deviceId);
        }

        private IRPlot FindPlot(Guid deviceId, Guid plotId) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            var device = FindDevice(deviceId);
            if (device != null) {
                return device.Find(plotId);
            }

            return null;
        }

        private void RemoveAllDevices() {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            IRPlotDevice[] devices = _devices.ToArray();
            _devices.Clear();

            foreach (var device in devices) {
                DeviceRemoved?.Invoke(this, new RPlotDeviceEventArgs(device));
            }
        }

        private int GetUnusedInstanceId() {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            // Pick the lowest number that isn't known to be used.
            // Note that some plot windows may technically exist but
            // have not been created yet in this instance of VS (it creates
            // them on demand when they are made visible).
            // By always picking the lowest 'unused' number, we
            // increase our chances of reusing a tool window that hasn't
            // been created yet, if there are any, rather than unnecessarily
            // create a new plot window.
            for (int i = 0; i < int.MaxValue; i++) {
                if (!_visualComponents.ContainsKey(i)) {
                    return i;
                }
            }

            return -1;
        }

        private void SetNextVisualComponent(int existingInstanceId) {
            InteractiveWorkflow.Shell.AssertIsOnMainThread();

            int[] indices = _unassignedVisualComponents.IndexWhere(component => component.InstanceId == existingInstanceId).ToArray();
            if (indices.Length == 1) {
                if (indices[0] > 0) {
                    // Desired visual component isn't the first in the list, so move it there.
                    var component = _unassignedVisualComponents[indices[0]];
                    _unassignedVisualComponents.RemoveAt(indices[0]);
                    _unassignedVisualComponents.Insert(0, component);
                }
            } else {
                // We didn't find the requested visual component.
                // Our internal state of unassigned visual components must be out of sync.
                Debug.Assert(false, "Could not find instance of plot window.");
            }
        }

        private void RSession_Mutated(object sender, EventArgs e) {
            RSessionMutatedAsync().DoNotWait();
        }

        private async Task RSessionMutatedAsync() {
            await _shell.SwitchToMainThreadAsync();

            try {
                var deviceId = await InteractiveWorkflow.RSession.GetActivePlotDeviceAsync();
                var device = FindDevice(deviceId);

                var deviceChanged = device != ActiveDevice;
                ActiveDevice = device;

                // Update all the devices in parallel
                var tasks = _devices.Select(RefreshDeviceNum);
                await Task.WhenAll(tasks);

                _interactiveWorkflow.ActiveWindow?.Container.UpdateCommandStatus(false);

                if (deviceChanged) {
                    ActiveDeviceChanged?.Invoke(this, new RPlotDeviceEventArgs(ActiveDevice));
                }
            } catch (RException) {
            } catch (OperationCanceledException) {
            }
        }

        private async Task RefreshDeviceNum(IRPlotDevice device) {
            var num = await InteractiveWorkflow.RSession.GetPlotDeviceNumAsync(device.DeviceId);
            device.DeviceNum = num ?? 0;
        }

        private void RSession_Connected(object sender, RConnectedEventArgs e) {
            RSessionConnectedAsync().DoNotWait();
        }

        private async Task RSessionConnectedAsync() {
            await _shell.SwitchToMainThreadAsync();

            RemoveAllDevices();

            foreach (var visualComponent in _assignedVisualComponents.Values) {
                visualComponent.Unassign();
                _unassignedVisualComponents.Add(visualComponent);
            }
            _assignedVisualComponents.Clear();
        }
    }
}
