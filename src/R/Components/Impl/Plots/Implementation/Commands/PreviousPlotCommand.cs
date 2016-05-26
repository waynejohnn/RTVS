﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.R.Components.Controller;
using Microsoft.R.Components.InteractiveWorkflow;

namespace Microsoft.R.Components.Plots.Implementation.Commands {
    internal sealed class PreviousPlotCommand : PlotCommand, IAsyncCommand {
        public PreviousPlotCommand(IRInteractiveWorkflow interactiveWorkflow) : base(interactiveWorkflow) {
        }

        public CommandStatus Status
        {
            get
            {
                if (InteractiveWorkflow.Plots.ActivePlotIndex > 0 &&
                    !IsInLocatorMode) {
                    return CommandStatus.SupportedAndEnabled;
                }

                return CommandStatus.Supported;
            }
        }

        public async Task<CommandResult> InvokeAsync() {
            try {
                await InteractiveWorkflow.Plots.PreviousPlotAsync();
            } catch (RPlotManagerException ex) {
                InteractiveWorkflow.Shell.ShowErrorMessage(ex.Message);
            } catch (OperationCanceledException) {
            }

            return CommandResult.Executed;
        }
    }
}
