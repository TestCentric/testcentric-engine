﻿// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using System.Collections.Generic;

namespace TestCentric.Tests.Fakes
{
    [NUnit.Engine.Extensibility.Extension]
    public class FakeNUnitTestEventListener : NUnit.Engine.ITestEventListener
    {
        public List<string> Output { get; } = new List<string>();

        public void OnTestEvent(string report)
        {
            Output.Add(report);
        }
    }
}
