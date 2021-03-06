// 
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

namespace Microsoft.OneGet.Utility.Collections {
    using System.Collections.Generic;
    using System.Linq;

    public class ReEnumerable<T> : MutableEnumerable<T> {
        private readonly IEnumerable<T> _source;
        private IEnumerator<T> _sourceIterator;

        public ReEnumerable(IEnumerable<T> source) {
            _source = source ?? new T[0];
        }

        public ReEnumerable(IEnumerator<T> sourceIterator) {
            _source = null;
            _sourceIterator = sourceIterator;
        }

        protected override bool ItemExists(int index) {
            if (index < List.Count) {
                return true;
            }

            lock (this) {
                if (_sourceIterator == null) {
                    _sourceIterator = _source.GetEnumerator();
                }

                while (_sourceIterator.MoveNext()) {
                    List.Add(_sourceIterator.Current);
                    if (index < List.Count) {
                        return true;
                    }
                }
            }
            return false;
        }

        public MutableEnumerable<T> Concat(IEnumerable<T> additionalItems) {
            return Enumerable.Concat(this, additionalItems).ReEnumerable();
        }
    }
}