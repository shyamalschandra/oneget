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

namespace Microsoft.PowerShell.OneGet.CmdLets {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using Microsoft.OneGet.Packaging;
    using Microsoft.OneGet.Utility.Async;
    using Microsoft.OneGet.Utility.Extensions;

    [Cmdlet(VerbsCommon.Set, Constants.Nouns.PackageSourceNoun, SupportsShouldProcess = true, DefaultParameterSetName = Constants.ParameterSets.SourceBySearchSet, HelpUri = "http://go.microsoft.com/fwlink/?LinkID=517141")]
    public sealed class SetPackageSource : CmdletWithProvider {
        [Parameter(ValueFromPipeline = true, ParameterSetName = Constants.ParameterSets.SourceByInputObjectSet, Mandatory = true)]
        public PackageSource InputObject;

        public SetPackageSource() : base(new[] {OptionCategory.Provider, OptionCategory.Source}) {
        }

        protected override IEnumerable<string> ParameterSets {
            get {
                return new[] {Constants.ParameterSets.SourceByInputObjectSet, Constants.ParameterSets.SourceBySearchSet};
            }
        }

        protected override void GenerateCmdletSpecificParameters(Dictionary<string, object> unboundArguments) {
            if (!IsInvocation) {
                var providerNames = PackageManagementService.AllProviderNames;
                var whatsOnCmdline = GetDynamicParameterValue<string[]>("ProviderName");
                if (whatsOnCmdline != null) {
                    providerNames = providerNames.Concat(whatsOnCmdline).Distinct();
                }

                DynamicParameterDictionary.AddOrSet("ProviderName", new RuntimeDefinedParameter("ProviderName", typeof(string), new Collection<Attribute> {
                    new ParameterAttribute {
                        ValueFromPipelineByPropertyName = true,
                        ParameterSetName = Constants.ParameterSets.SourceBySearchSet,
                    },
                    new AliasAttribute("Provider"),
                    new ValidateSetAttribute(providerNames.ToArray())
                }));
            }
            else {
                DynamicParameterDictionary.AddOrSet("ProviderName", new RuntimeDefinedParameter("ProviderName", typeof(string), new Collection<Attribute> {
                    new ParameterAttribute {
                        ValueFromPipelineByPropertyName = true,
                        ParameterSetName = Constants.ParameterSets.SourceBySearchSet,
                    },
                    new AliasAttribute("Provider")
                }));
            }
        }

        [Alias("SourceName")]
        [Parameter(Position = 0, ParameterSetName = Constants.ParameterSets.SourceBySearchSet)]
        public string Name {get; set;}

        [Parameter(ParameterSetName = Constants.ParameterSets.SourceBySearchSet)]
        public string Location {get; set;}

        [Parameter]
        public string NewLocation {get; set;}

        [Parameter]
        public string NewName {get; set;}

        [Parameter]
        public SwitchParameter Trusted {get; set;}

        public override IEnumerable<string> Sources {
            get {
                if (Name.IsEmptyOrNull() && Location.IsEmptyOrNull()) {
                    return Microsoft.OneGet.Constants.Empty;
                }

                return new[] {
                    Name ?? Location
                };
            }
        }

        /// <summary>
        ///     This can be used when we want to override some of the functions that are passed
        ///     in as the implementation of the IHostApi (ie, 'request object').
        ///     Because the DynamicInterface DuckTyper will use all the objects passed in in order
        ///     to implement a given API, if we put in delegates to handle some of the functions
        ///     they will get called instead of the implementation in the current class. ('this')
        /// </summary>
        private object WithUpdatePackageSource {
            get {
                return new object[] {
                    new {
                        // override the GetOptionKeys and the GetOptionValues on the fly.

                        GetOptionKeys = new Func<IEnumerable<string>>(() => {
                            return GetOptionKeys().ConcatSingleItem("IsUpdatePackageSource").ByRef();
                        }),

                        GetOptionValues = new Func<string, IEnumerable<string>>((key) => {
                            if (key != null && key.EqualsIgnoreCase("IsUpdatePackageSource")) {
                                return "true".SingleItemAsEnumerable().ByRef();
                            }
                            return GetOptionValues(key);                        
                        })
                    },
                    this,
                };
            }
        }

        private void UpdatePackageSource(PackageSource source) {
            if (string.IsNullOrEmpty(NewName)) {
                // this is a replacement of an existing package source, we're *not* changing the name. (easy)

                foreach (var src in source.Provider.AddPackageSource(string.IsNullOrEmpty(NewName) ? source.Name : NewName, string.IsNullOrEmpty(NewLocation) ? source.Location : NewLocation, Trusted, WithUpdatePackageSource)) {
                    WriteObject(src);
                }

            } else {
                // we're renaming a source. 
                // a bit more messy at this point
                // create a new package source first
                
                foreach (var src in source.Provider.AddPackageSource(NewName, string.IsNullOrEmpty(NewLocation) ? source.Location : NewLocation, Trusted.IsPresent ? Trusted.ToBool() : source.IsTrusted, this)) {
                    WriteObject(src);
                }

                // remove the old one.
                source.Provider.RemovePackageSource(source.Name, this).Wait();
            }
        }

        public override bool ProcessRecordAsync() {
            if (IsSourceByObject) {
                // we've already got the package source
                UpdatePackageSource(InputObject);
                return true;
            }

            if (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Location)) {
                Error(Constants.Errors.NameOrLocationRequired);
                return false;
            }

            // otherwise, we're just changing a source by name
            var prov = SelectedProviders.ToArray();

            if (Stopping) {
                return false;
            }

            if (prov.Length == 0) {
                if (ProviderName.IsNullOrEmpty() || string.IsNullOrEmpty(ProviderName[0])) {
                    return Error(Constants.Errors.UnableToFindProviderForSource, Name ?? Location);
                }
                return Error(Constants.Errors.UnknownProvider, ProviderName[0]);
            }

            if (prov.Length > 0) {
                var sources = prov.SelectMany(each => each.ResolvePackageSources(SuppressErrorsAndWarnings).Where(source => source.IsRegistered &&
                                                                                                       (Name == null || source.Name.EqualsIgnoreCase(Name)) || (Location == null || source.Location.EqualsIgnoreCase(Location))).ToArray()).ToArray();

                if (sources.Length == 0) {
                    return Error(Constants.Errors.SourceNotFound, Name);
                }

                if (sources.Length > 1) {
                    return Error(Constants.Errors.SourceFoundInMultipleProviders, Name, prov.Select(each => each.ProviderName).JoinWithComma());
                }

                UpdatePackageSource(sources[0]);
            }
            return true;
        }
    }
}