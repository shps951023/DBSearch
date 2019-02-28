namespace DBSearch
{
    internal class MatchColumnCountModel
    {
        public object ColumnValue { get; set; }
        public int MatchCount { get; set; }
    }
    internal class ColumnsSchmaModel
    {
        public string TABLE_CATALOG { get; set; }
        public string TABLE_NAME { get; set; }
        public string TABLE_SCHEMA { get; set; }
        public string TABLE_TYPE { get; set; }
        public string COLUMN_NAME { get; set; }
        public string IS_NULLABLE { get; set; }
        public string DATA_TYPE { get; set; }
    }
    public enum ComparisonOperator
    {
        Equal, Like
    }
}
