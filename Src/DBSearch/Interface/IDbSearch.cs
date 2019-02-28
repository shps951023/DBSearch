namespace DBSearch
{
    using System.Collections.Generic;

    interface IDbSearch //use interface for common method
    {
        IEnumerable<MatchColumnModel> Search();
    }

    abstract class DbSearchBase : IDbSearch
    {
        public abstract IEnumerable<MatchColumnModel> Search();
    }
}
