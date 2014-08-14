﻿// 
//  Copyright (c) Microsoft Corporation. All rights reserved. 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  

namespace OneGet.PowerShell.Module.Test {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.OneGet.Utility.Extensions;
    using Microsoft.OneGet.Utility.Platform;
    using Microsoft.OneGet.Utility.PowerShell;
    using Xunit;

    public class TestDynamicPowerShell {
        private string NuGetPath {
            get {
                var systemBase = KnownFolders.GetFolderPath(KnownFolder.CommonApplicationData);
                Assert.False(string.IsNullOrEmpty(systemBase), "Known Folder CommonApplicationData is null");

                var nugets = NugetsInPath(Path.Combine(systemBase, "oneget", "ProviderAssemblies"));
                var first = nugets.FirstOrDefault();
                if (first != null) {
                    return first;
                }

                var userBase = KnownFolders.GetFolderPath(KnownFolder.LocalApplicationData);
                Assert.False(string.IsNullOrEmpty(userBase));

                nugets = NugetsInPath(Path.Combine(userBase, "oneget", "ProviderAssemblies"));
                first = nugets.FirstOrDefault();
                if (first != null) {
                    return first;
                }

                return null;
            }
        }

        private bool IsNuGetInstalled {
            get {
                return NuGetPath != null;
            }
        }

        private dynamic NewPowerShellSession {
            get {
                dynamic p = new DynamicPowershell();
                DynamicPowershellResult result = p.ImportModule(".\\oneget.psd1");
                Assert.False(result.IsFailing, "unable to import '.\\oneget.psd1  (PWD:'{0}')".format(Environment.CurrentDirectory));
                return p;
            }
        }

        [Fact]
        public void TestGetPackageProvider() {
            
            var PS = NewPowerShellSession;
            
            DynamicPowershellResult result = PS.GetPackageProvider();
            var items = result.ToArray();

            foreach (dynamic i in items) {
                Console.WriteLine(i.Name);
            }
        }

        [Fact]
        public void TestGetPackageProviderByName() {
            var PS = NewPowerShellSession;

            DynamicPowershellResult result = PS.GetPackageProvider(Name: "NuGet", ForceBootstrap: true, IsTesting: true);
            Assert.False(result.IsFailing);

            var items = result.ToArray();
            Assert.Equal(1, items.Length);
        }

        private bool IsDllOrExe(string path) {
            return path.ToLower().EndsWith(".exe") || path.ToLower().EndsWith(".dll");
        }

        private IEnumerable<string> FilenameContains(IEnumerable<string> paths, string value) {
            foreach (var item in paths) {
                if (item.IndexOf(value, StringComparison.OrdinalIgnoreCase) > -1) {
                    yield return item;
                }
            }
        }

        private IEnumerable<string> NugetsInPath(string folder) {
            if (Directory.Exists(folder)) {
                var files = Directory.EnumerateFiles(folder).ToArray();

                return FilenameContains(files, "nuget").Where(IsDllOrExe);
            }
            return Enumerable.Empty<string>();
        }

        private void DeleteNuGet() {
            var systemBase = KnownFolders.GetFolderPath(KnownFolder.CommonApplicationData);
            Assert.False(string.IsNullOrEmpty(systemBase));

            var nugets = NugetsInPath(Path.Combine(systemBase, "oneget", "ProviderAssemblies"));
            foreach (var nuget in nugets) {
                nuget.TryHardToDelete();
            }

            var userBase = KnownFolders.GetFolderPath(KnownFolder.LocalApplicationData);
            Assert.False(string.IsNullOrEmpty(userBase));

            nugets = NugetsInPath(Path.Combine(userBase, "oneget", "ProviderAssemblies"));
            foreach (var nuget in nugets) {
                nuget.TryHardToDelete();
            }
        }

        [Fact]
        public void TestBootstrapNuGet() {
            // delete any copies of nuget if they are installed.
            if (IsNuGetInstalled) {
                DeleteNuGet();
            }

            // verify that nuget is not installed.
            Assert.False(IsNuGetInstalled, "NuGet is still installed at :".format(NuGetPath));

            var PS = NewPowerShellSession;

            // ask onget for the nuget package provider, bootstrapping if necessary
            DynamicPowershellResult result = PS.GetPackageProvider(Name: "NuGet", ForceBootstrap: true, IsTesting: true);
            Assert.False(result.IsFailing);

            // did we get back one item?
            var items = result.ToArray();
            Assert.Equal(1, items.Length);

            // and is the nuget.exe where we expect it?
            Assert.True(IsNuGetInstalled);
        }
    }
}