
namespace SemanticQuestion10K.Catalog
{
    internal class Document
    {
        public Document() { }

        public string Jurisdiction { get; set; }

        /// <summary>
        /// Publisher full company name
        /// </summary>
        public string Publisher { get; set; }

        /// <summary>
        /// Entity identifier
        /// CIK for SEC, LEI (Legal Entity Identifier) for ESEF
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Form Title, 10-K and EU Annual Reports for now
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Reference date (year end)
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Path to public submission, usefull in case of web crawling 
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Local path to the submission text loaded into the model
        /// </summary>
        public string Path { get; set; }

    }
}
