// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

namespace TestCentric.Engine.Services
{
    class TestFilterService : Service, ITestFilterService
    {
        public ITestFilterBuilder GetTestFilterBuilder()
        {
            return new TestFilterBuilder();
        }
    }
}
