namespace DBSearch
{
    internal class ColumnGroupResultViewModel
    {
        public object ColumnValue { get; set; }
        public int MatchCount { get; set; }
    }
    internal class TableSchmaModel
    {
        public string TABLE_CATALOG { get; set; }
        public string TABLE_NAME { get; set; }
        public string TABLE_SCHEMA { get; set; }
        public string TABLE_TYPE { get; set; }
        public string COLUMN_NAME { get; set; }
        public int? ORDINAL_POSITION { get; set; }
        public string IS_NULLABLE { get; set; }
        public string DATA_TYPE { get; set; }
    }
    public enum ComparisonOperator
    {
        Equal, Like
    }
}
