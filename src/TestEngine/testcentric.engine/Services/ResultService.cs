// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric contributors.
// Licensed under the MIT License. See LICENSE file in root directory.
// ***********************************************************************

using NUnit.Engine;
using System;
using System.Collections.Generic;
using TestCentric.Engine.Extensibility;
using TestCentric.Extensibility;

namespace TestCentric.Engine.Services
{
    class ResultService : Service, IResultService
    {
        private readonly string[] BUILT_IN_FORMATS = new string[] { "nunit3", "cases", "user" };
        private List<IExtensionNode> _extensionNodes = new List<IExtensionNode>();

        private string[] _formats;
        public string[] Formats
        {
            get
            {
                if (_formats == null)
                {
                    var formatList = new List<string>(BUILT_IN_FORMATS);

                    foreach (var node in _extensionNodes)
                        foreach (var format in node.GetValues("Format"))
                            formatList.Add(format);

                    _formats = formatList.ToArray();
                }

                return _formats;
            }
        }

        /// <summary>
        /// Gets a ResultWriter for a given format and set of arguments.
        /// </summary>
        /// <param name="format">The name of the format to be used</param>
        /// <param name="args">A set of arguments to be used in constructing the writer or null if non arguments are needed</param>
        /// <returns>An IResultWriter</returns>
        public IResultWriter GetResultWriter(string format, object[] args)
        {
            switch (format)
            {
                case "nunit3":
                    return new NUnit3XmlResultWriter();
                case "cases":
                    return new TestCaseResultWriter();
                case "user":
                    return new XmlTransformResultWriter(args);
                default:
                    foreach (var node in _extensionNodes)
                        foreach (var supported in node.GetValues("Format"))
                            if (supported == format)
                            {
                                // HACK until we change IExtensionNode definition or use ExtensionNode directly
                                var obj = ((ExtensionNode)node).ExtensionObject;
                                return obj as IResultWriter ?? new NUnitResultWriterWrapper(obj as NUnit.Engine.Extensibility.IResultWriter);
                            }
                    return null;
            }
        }

        public override void StartService()
        {
            try
            {
                var extensionService = ServiceContext.GetService<ExtensionService>();

                if (extensionService != null && extensionService.Status == ServiceStatus.Started)
                {
                    _extensionNodes.AddRange(extensionService.GetExtensionNodes<IResultWriter>());
                    _extensionNodes.AddRange(extensionService.GetExtensionNodes<NUnit.Engine.Extensibility.IResultWriter>());
                }

                // If there is no extension service, we start anyway using builtin writers
                Status = ServiceStatus.Started;
            }
            catch
            {
                Status = ServiceStatus.Error;
                throw;
            }
        }
    }
}
