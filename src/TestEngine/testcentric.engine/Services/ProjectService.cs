// ***********************************************************************
// Copyright (c) Charlie Poole and TestCentric Engine contributors.
// Licensed under the MIT License. See LICENSE.txt in root directory.
// ***********************************************************************

using System.Collections.Generic;
using System.IO;
using TestCentric.Common;
using TestCentric.Engine.Extensibility;

namespace TestCentric.Engine.Services
{
    /// <summary>
    /// Summary description for ProjectService.
    /// </summary>
    public class ProjectService : Service, IProjectService
    {
        Dictionary<string, ExtensionNode> _extensionIndex = new Dictionary<string, ExtensionNode>();

        public bool CanLoadFrom(string path)
        {
            ExtensionNode node = GetNodeForPath(path);
            if (node != null)
            {
                var loader = node.ExtensionObject as IProjectLoader;
                if (loader.CanLoadFrom(path))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Expands a TestPackage based on a known project format, populating it
        /// with the project contents and any settings the project provides.
        /// Note that the package file path must be checked to ensure that it is
        /// a known project format before calling this method.
        /// </summary>
        /// <param name="package">The TestPackage to be expanded</param>
        public void ExpandProjectPackage(TestPackage package)
        {
            Guard.ArgumentNotNull(package, "package");
            Guard.ArgumentValid(package.SubPackages.Count == 0, "Package is already expanded", "package");

            string path = package.FullName;
            if (!File.Exists(path))
                return;

            IProject project = LoadFrom(path);
            Guard.ArgumentValid(project != null, "Unable to load project " + path, "package");

            string configName = package.GetSetting(EnginePackageSettings.ActiveConfig, (string)null); // Need RunnerSetting
            TestPackage tempPackage = project.GetTestPackage(configName);

            // The original package held overrides, so don't change them, but
            // do apply any settings specified within the project itself.
            foreach (string key in tempPackage.Settings.Keys)
                if (!package.Settings.ContainsKey(key)) // Don't override settings from command line
                    package.Settings[key] = tempPackage.Settings[key];

            foreach (var subPackage in tempPackage.SubPackages)
                package.AddSubPackage(subPackage);

            // If no config is specified (by user or by the project loader) check
            // to see if one exists in same directory as the package. If so, we
            // use it. If not, each assembly will use its own config, if present.
            if (!package.Settings.ContainsKey(EnginePackageSettings.ConfigurationFile))
            {
                var packageConfig = Path.ChangeExtension(path, ".config");
                if (File.Exists(packageConfig))
                    package.Settings[EnginePackageSettings.ConfigurationFile] = packageConfig;
            }
        }

        public override void StartService()
        {
            if (Status == ServiceStatus.Stopped)
            {
                try
                {
                    var extensionService = ServiceContext.GetService<ExtensionService>();

                    if (extensionService == null)
                        Status = ServiceStatus.Started;
                    else if (extensionService.Status != ServiceStatus.Started)
                        Status = ServiceStatus.Error;
                    else
                    {
                        Status = ServiceStatus.Started;

                        foreach (var node in extensionService.GetExtensionNodes<IProjectLoader>())
                        {
                            foreach (string ext in node.GetValues("FileExtension"))
                            {
                                if (ext != null)
                                {
                                    if (_extensionIndex.ContainsKey(ext))
                                        throw new NUnitEngineException(string.Format("ProjectLoader extension {0} is already handled by another extension.", ext));

                                    _extensionIndex.Add(ext, node);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // TODO: Should we just ignore any addin that doesn't load?
                    Status = ServiceStatus.Error;
                    throw;
                }
            }
        }

        public IProject LoadFrom(string path)
        {
            if (File.Exists(path))
            {
                ExtensionNode node = GetNodeForPath(path);
                if (node != null)
                {
                    var loader = node.ExtensionObject as IProjectLoader;
                    if (loader.CanLoadFrom(path))
                        return loader.LoadFrom(path);
                }
            }

            return null;
        }

        private ExtensionNode GetNodeForPath(string path)
        {
            var ext = Path.GetExtension(path);

            if (string.IsNullOrEmpty(ext) || !_extensionIndex.ContainsKey(ext))
                return null;

            return _extensionIndex[ext];
        }
    }
}
