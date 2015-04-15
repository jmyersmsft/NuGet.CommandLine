﻿using NuGet.PackageManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.Common
{
    public class SelfUpdater
    {
        private const string NuGetCommandLinePackageId = "NuGet.CommandLine";
        private const string NuGetExe = "NuGet.exe";

        public IConsole Console { get; set; }
        private SourceRepository _source;
       

        public SelfUpdater(SourceRepository source)
        {
            _source = source;
        }
        public async Task UpdateSelf()
        {
            Assembly assembly = typeof(SelfUpdater).Assembly;
            var version = GetNuGetVersion(assembly) ?? new NuGetVersion(assembly.GetName().Version);
            await SelfUpdate(assembly.Location, version);
        }

        internal async Task SelfUpdate(string exePath, NuGetVersion version)
        {
            Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandCheckingForUpdates"), NuGetConstants.DefaultFeedUrl);
            var metadataResource = _source.GetResource<MetadataResource>();
            if (metadataResource != null)
            {
                ResolutionContext resolutionContext = new ResolutionContext();
                var latestVersionKeyPair = await metadataResource.GetLatestVersions(new List<string>() { NuGetCommandLinePackageId },
                    resolutionContext.IncludePrerelease, resolutionContext.IncludeUnlisted, CancellationToken.None);
                var lastetVersion = latestVersionKeyPair.FirstOrDefault().Value;

                if (version >= lastetVersion)
                {
                    Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandNuGetUpToDate"));
                }
                else
                {
                    Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandUpdatingNuGet"), lastetVersion.Version.ToString());

                    // Get NuGet.exe file
                    using (var targetPackageStream = new MemoryStream())
                    {
                        await PackageDownloader.GetPackageStreamAsync(_source, new PackageIdentity(NuGetCommandLinePackageId, lastetVersion), targetPackageStream, CancellationToken.None);
                        // If for some reason this package doesn't have NuGet.exe then we don't want to use it
                        if (targetPackageStream == null)
                        {
                            throw new CommandLineException(LocalizedResourceManager.GetString("UpdateCommandUnableToLocateNuGetExe"));
                        }

                        // Get the exe path and move it to a temp file (NuGet.exe.old) so we can replace the running exe with the bits we got 
                        // from the package repository
                        string renamedPath = exePath + ".old";
                        Move(exePath, renamedPath);

                        // Update the file
                        UpdateFile(exePath, targetPackageStream);
                        Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandUpdateSuccessful"));
                    }
                }
            }
        }

        protected virtual void UpdateFile(string exePath, MemoryStream targetPackageStream)
        {
            using (Stream fromStream = targetPackageStream, toStream = File.Create(exePath))
            {
                fromStream.CopyTo(toStream);
            }
        }

        protected virtual void Move(string oldPath, string newPath)
        {
            try
            {
                if (File.Exists(newPath))
                {
                    File.Delete(newPath);
                }
            }
            catch (FileNotFoundException)
            {

            }

            File.Move(oldPath, newPath);
        }
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want this method to throw.")]
        internal static NuGetVersion GetNuGetVersion(ICustomAttributeProvider assembly)
        {
            try
            {
                var assemblyInformationalVersion = 
                (AssemblyInformationalVersionAttribute)assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), inherit: false).FirstOrDefault();

                return new NuGetVersion(assemblyInformationalVersion.InformationalVersion);
            }
            catch
            {
                // Don't let GetCustomAttributes throw.
            }
            return null;
        }
    }
}
