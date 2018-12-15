using System;

namespace CM.Caching.Tests
{
    public class Source : ISource
    {
        public string GetData(string myParameter)
        {
            throw new NotImplementedException();
        }

        public int? GetSomeMoreData(int? myParameter1, bool? optionalParameter = null)
        {
            throw new NotImplementedException();
        }
    }
}
