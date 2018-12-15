namespace CM.Caching.Tests
{
    public interface ISource
    {
        string GetData(string myParameter);
        int? GetSomeMoreData(int? myParameter1, bool? optionalParameter = null);
    }
}
