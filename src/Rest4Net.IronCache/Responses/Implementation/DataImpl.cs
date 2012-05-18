﻿using System.Collections.Generic;

namespace Rest4Net.IronCache.Responses.Implementation
{
    internal class DataImpl<TInterface, TClass> where TClass : class, TInterface
    {
        private readonly List<TClass> data;

        public IEnumerable<TInterface> Data
        {
            get { return data; }
        }
    }
}
