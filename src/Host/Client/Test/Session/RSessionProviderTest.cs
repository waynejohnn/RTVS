﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Common.Core.Test.Fakes.Shell;
using Microsoft.R.Host.Client.Session;
using Microsoft.UnitTests.Core.FluentAssertions;
using Microsoft.UnitTests.Core.Threading;
using Microsoft.UnitTests.Core.XUnit;
using Xunit;

namespace Microsoft.R.Host.Client.Test.Session {
    [Category.R.Session]
    public class RSessionProviderTest {
        [Test]
        public void Lifecycle() {
            var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal());
            // ReSharper disable once AccessToDisposedClosure
            Action a = () => sessionProvider.GetOrCreate(new Guid());
            a.ShouldNotThrow();

            sessionProvider.Dispose();
            a.ShouldThrow<InvalidOperationException>();
        }

        [Test]
        public void GetOrCreate() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session1 = sessionProvider.GetOrCreate(guid);
                session1.Should().NotBeNull();

                var session2 = sessionProvider.GetOrCreate(guid);
                session2.Should().BeSameAs(session1);

                session1.Dispose();
                var session3 = sessionProvider.GetOrCreate(guid);
                session3.Should().NotBeSameAs(session1);
                session3.Id.Should().NotBe(session1.Id);
            }
        }

        [Test]
        public void ParallelAccess() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guids = new[] { new Guid(), new Guid() };
                ParallelTools.Invoke(100, i => {
                    var session = sessionProvider.GetOrCreate(guids[i % 2]);
                    session.Dispose();
                });
            }
        }

        [Test]
        public async Task ConnectWhenSwitching() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid1 = new Guid();
                var guid2 = new Guid();
                var session1 = sessionProvider.GetOrCreate(guid1);
                var session2 = sessionProvider.GetOrCreate(guid2);
                var switchTask = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(ConnectWhenSwitching));

                var startHost1Task = Task.Delay(50).ContinueWith(t => session1.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session1)
                }, null, 1000)).Unwrap();

                var startHost2Task = Task.Delay(100).ContinueWith(t => session2.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session2)
                }, null, 1000)).Unwrap();

                await ParallelTools.WhenAll(30000, switchTask, startHost1Task, startHost2Task);

                startHost1Task.Should().BeRanToCompletion();
                startHost2Task.Should().BeRanToCompletion();

                await ParallelTools.WhenAll(30000, session1.HostStarted, session2.HostStarted);
            }
        }

        [Test]
        public async Task ConnectWhenSwitching_SwitchFailed() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(ConnectWhenSwitching_SwitchFailed));
                var switchTask = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(ConnectWhenSwitching_SwitchFailed) + "1", @"\\JHF\F\R");
                await Task.Yield();
                var session = sessionProvider.GetOrCreate(new Guid());
                var startHostTask = session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 1000);

                await ParallelTools.WhenAll(switchTask, startHostTask);

                startHostTask.Should().BeRanToCompletion();
                switchTask.Should().BeRanToCompletion();
                switchTask.Result.Should().BeFalse();
            }
        }

        [Test]
        public async Task SwitchWhenConnecting() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session = sessionProvider.GetOrCreate(guid);
                session.RestartOnBrokerSwitch = true;

                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchWhenConnecting));

                var startHostTask = session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 1000);

                await Task.Yield();

                var switch1Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchWhenConnecting) + "1");
                var switch2Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchWhenConnecting) + "2");

                await ParallelTools.WhenAll(startHostTask, switch1Task, switch2Task);

                switch1Task.Status.Should().Be(TaskStatus.RanToCompletion);
                switch2Task.Status.Should().Be(TaskStatus.RanToCompletion);
                startHostTask.Status.Should().Be(TaskStatus.RanToCompletion);

                await session.HostStarted.Should().BeCompletedAsync();
            }
        }

        [Test]
        public async Task SwitchMultipleSessions() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchMultipleSessions)).Should().BeCompletedAsync();

                var sessions = new IRSession[4];
                for (var i = 0; i < sessions.Length; i++) {
                    sessions[i] = sessionProvider.GetOrCreate(new Guid());
                    sessions[i].RestartOnBrokerSwitch = true;
                }

                await ParallelTools.WhenAll(30000, sessions.Select((s, i) => s.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(sessions) + i
                }, null, 30000)).ToArray());

                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchMultipleSessions) + "1").Should().BeCompletedAsync();

                sessions.Should().OnlyContain(s => s.IsHostRunning);
            }
        }

        [Test]
        public async Task SwitchToInvalid() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session = sessionProvider.GetOrCreate(guid);
                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToInvalid));
                await session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 1000);

                var switch1Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToInvalid) + "1", @"\\JHF\F\R");
                await Task.Yield();
                var switch2Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToInvalid) + "2");
                await ParallelTools.WhenAll(30000, switch1Task, switch2Task);

                switch1Task.Should().BeRanToCompletion();
                switch1Task.Result.Should().BeFalse();
                switch2Task.Should().BeRanToCompletion();
                switch2Task.Result.Should().BeTrue();
            }
        }

        [Test]
        public async Task SwitchWhileEnsureHostStartedAsyncFailed() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session = sessionProvider.GetOrCreate(guid);
                session.RestartOnBrokerSwitch = true;

                var startTask = session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 1000);
                await Task.Yield();
                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchWhileEnsureHostStartedAsyncFailed));

                startTask.Should().BeCanceled();
                session.IsHostRunning.Should().BeTrue();
            }
        }

        [Test]
        public async Task SwitchToTheSameBroker() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session = sessionProvider.GetOrCreate(guid);
                session.RestartOnBrokerSwitch = true;

                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToTheSameBroker));
                await session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 1000);

                var switch1Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToTheSameBroker) + "1");
                var switch2Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToTheSameBroker) + "1");
                await ParallelTools.WhenAll(30000, switch1Task, switch2Task);

                switch1Task.Status.Should().Be(TaskStatus.RanToCompletion);
                switch2Task.Status.Should().Be(TaskStatus.RanToCompletion);
            }
        }

        [Test]
        public async Task SwitchToTheSameBroker_NoSessions() {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var switch1Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToTheSameBroker_NoSessions));
                var switch2Task = sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchToTheSameBroker_NoSessions));
                await ParallelTools.WhenAll(30000, switch1Task, switch2Task);

                switch1Task.Status.Should().Be(TaskStatus.RanToCompletion);
                switch2Task.Status.Should().Be(TaskStatus.RanToCompletion);
            }
        }

        [InlineData(0)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(400)]
        [InlineData(600)]
        [InlineData(800)]
        [CompositeTest]
        public async Task SwitchBrokerWithCancellation(int timeout) {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session = sessionProvider.GetOrCreate(guid);
                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchBrokerWithCancellation));
                await session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 1000);

                var result = await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchBrokerWithCancellation) + "1",
                    cancellationToken: new CancellationTokenSource(timeout).Token);

                result.Should().BeFalse();
            }
        }

        [InlineData(0, true)]
        [InlineData(0, false)]
        [InlineData(10, true)]
        [InlineData(10, false)]
        [InlineData(50, true)]
        [InlineData(50, false)]
        [InlineData(100, true)]
        [InlineData(100, false)]
        [InlineData(200, true)]
        [InlineData(200, false)]
        [InlineData(400, true)]
        [InlineData(400, false)]
        [InlineData(600, true)]
        [InlineData(600, false)]
        [InlineData(800, true)]
        [InlineData(800, false)]
        [CompositeTest]
        public async Task SwitchBroker_DisposeSession(int timeout, bool restartOnBrokerSwitch) {
            using (var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal())) {
                var guid = new Guid();
                var session = sessionProvider.GetOrCreate(guid);
                session.RestartOnBrokerSwitch = restartOnBrokerSwitch;

                await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchBrokerWithCancellation));
                await session.EnsureHostStartedAsync(new RHostStartupInfo {
                    Name = nameof(session)
                }, null, 5000);

                var sessionDisposeTask = Task.Delay(timeout).ContinueWith(t => session.Dispose());
                var result = await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchBrokerWithCancellation) + "1");
                await sessionDisposeTask;

                result.Should().BeTrue();
            }
        }

        [InlineData(0)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(400)]
        [InlineData(600)]
        [InlineData(800)]
        [CompositeTest]
        public async Task SwitchBroker_DisposeSessionProvider(int timeout) {
            var sessionProvider = new RSessionProvider(TestCoreServices.CreateReal());
            var guid = new Guid();
            var session1 = sessionProvider.GetOrCreate(guid);
            var session2 = sessionProvider.GetOrCreate(guid);
            session2.RestartOnBrokerSwitch = true;

            await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchBrokerWithCancellation));
            await session1.EnsureHostStartedAsync(new RHostStartupInfo {
                Name = nameof(session1)
            }, null, 5000).Should().BeCompletedAsync();
            await session2.HostStarted.Should().BeCompletedAsync();

            var sessionProviderDisposeTask = Task.Delay(timeout).ContinueWith(t => sessionProvider.Dispose());
            var result = await sessionProvider.TrySwitchBrokerAsync(nameof(RSessionProviderTest) + nameof(SwitchBrokerWithCancellation) + "1");
            await sessionProviderDisposeTask;

            result.Should().BeFalse();
        }
    }
}
