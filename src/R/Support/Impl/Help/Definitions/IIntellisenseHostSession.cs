﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.R.Host.Client;

namespace Microsoft.R.Support.Help.Definitions {
    public interface IIntellisenseHostSession : IDisposable {
        IRSession Session { get; }
        Task CreateSessionAsync();
    }
}
