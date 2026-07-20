// AssemblyInfo.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

using Xunit;

// These are heavyweight interactive tests: each launches a real SampleWpfApp window and either injects
// the payload (EndToEndTests / WalkthroughCaptureTests / DrivingE2ETests) or drives it out-of-process
// (UiaDriverTests / PrintWindowCaptureTests). The injected payload opens a single shared
// %AppData%\Snoop\SnoopLog.txt, so two injecting tests running concurrently collide on that file's
// lock and one fails with PayloadLoadFailed. Serialize the whole project — these tests must not run in
// parallel with one another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
