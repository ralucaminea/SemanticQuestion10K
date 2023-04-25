
namespace SemanticQuestion10K.Catalog
{
    internal class Filings
    {
        public IList<Document> Documents { get; set; }
        public string Version { get; set; }

        public Filings() {
            Documents = new List<Document>();
        }
    }
}
