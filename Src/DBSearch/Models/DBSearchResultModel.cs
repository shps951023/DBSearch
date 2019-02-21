namespace DBSearch
{
    public class MatchColumnModel
    {
        public string TableName { get; set; }
        public string Schema { get; set; }
        public string Database { get; set; }
        public object SearchValue { get; set; }
        public int MatchCount { get; set; }
        public string ColumnName { get; set; }
        public object ColumnValue { get; set; } //for like where
        public string ColumnQuerySQL { get; set; }
    }
}
