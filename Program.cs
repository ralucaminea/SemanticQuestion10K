
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using RepoUtils;
using BlingFire;
using SemanticQuestion10K.Catalog;
using System.Diagnostics;

namespace SemanticQuestion10K
{
    internal class Program
    {
        const string DataPath = "docs";
        const string IndexFile = "index.json";

        const string EmbeddingServiceId = "text-embedding-ada-002";
        const string CompletionServiceId = "text-davinci-003";

        const string QDRANT_ENDPOINT = "QDRANT_ENDPOINT";
        const string AZURE_OPENAI_ENDPOINT = "AZURE_OPENAI_ENDPOINT";
        const string OPENAI_API_KEY = "OPENAI_API_KEY";
        const string AZURE_OPENAI_KEY = "AZURE_OPENAI_KEY";

        static void Main(string[] args)
        {
            bool parse = false;
            bool question = false;
            bool useAzureOpenAI = false;

            //loop through args with an integer
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--parse") parse = true;
                if (args[i] == "--question") question = true;
                if (args[i] == "--azure") useAzureOpenAI = true;
            }

            QdrantMemoryStore memoryStore = new QdrantMemoryStore(Env.Var(QDRANT_ENDPOINT), 6333, vectorSize: 1536, ConsoleLogger.Log);

            var kernel = Kernel.Builder
                .WithLogger(ConsoleLogger.Log)
                .Configure(c => {
                    if (useAzureOpenAI)
                    {
                        c.AddAzureTextEmbeddingGenerationService(EmbeddingServiceId, EmbeddingServiceId, Env.Var(AZURE_OPENAI_ENDPOINT), Env.Var(AZURE_OPENAI_KEY));
                    }
                    else
                    {
                        c.AddOpenAITextEmbeddingGenerationService(EmbeddingServiceId, EmbeddingServiceId, Env.Var(OPENAI_API_KEY));
                    }
                })
                .WithMemoryStorage(memoryStore)
                .Build();

            if (useAzureOpenAI)
            {
                kernel.Config
                    .AddAzureTextCompletionService(CompletionServiceId, CompletionServiceId, Env.Var(AZURE_OPENAI_ENDPOINT), Env.Var(AZURE_OPENAI_KEY));
            }
            else
            {
                kernel.Config
                    .AddOpenAITextCompletionService(CompletionServiceId, CompletionServiceId, Env.Var(OPENAI_API_KEY));
            }

            if (question) RunAsync(kernel).Wait();

            if (parse) IndexFiles(kernel);
        }

        static void IndexFiles(IKernel kernel)
        {
            var filings = JsonFileReader.Read<Filings>($"{DataPath}\\{IndexFile}");
            foreach (Document entry in filings.Documents)
            {
                string memoryCollectionName = $"{entry.Publisher} {entry.Title}";
                string textfile = $"{DataPath}\\{entry.Date.Year}\\{entry.Jurisdiction}\\{entry.Path}.txt";
                string sourceName = $"{entry.Path}{entry.Date.Year}";
  
                ParseText(kernel, textfile, memoryCollectionName, sourceName).Wait();
            }
        }

        static async Task ParseText(IKernel kernel, string file, string memoryCollectionName, string sourceName)
        {
            string text = File.ReadAllText(file);
            var allsentences = BlingFireUtils.GetSentences(text);

            int i = 0;
            foreach (var s in allsentences)
            {
                await kernel.Memory.SaveReferenceAsync(
                    collection: memoryCollectionName,
                    description: s,
                    text: s,
                    externalId: i.ToString(),
                    externalSourceName: sourceName
                );
                Console.WriteLine($"   {memoryCollectionName} sentence {++i} saved");
            }
        }

        public static async Task RunAsync(IKernel kernel)
        {
            var filings = JsonFileReader.Read<Filings>($"{DataPath}\\{IndexFile}");
            
            string selection = string.Empty;
            Catalog.Document document = null;
            while (true)
            {
                document = GetDocumentSelection(filings, selection);
                await QuestionDocument(kernel, document);
            }
        }

        private static async Task QuestionDocument(IKernel kernel, Document document)
        {
            string memoryCollectionName = $"{document.Publisher} {document.Title}";
            string sourceName = $"{document.Path}{document.Date.Year}";
            var input = $"{document.Publisher} {document.Date.Year} {document.Title}";

            Console.WriteLine($"\nHi, welcome to {input}. What would you like to know?\n");
            while (true)
            {
                Console.Write("User: ");
                var query = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(query)) { break; }
                
                var results = kernel.Memory.SearchAsync(memoryCollectionName, query, limit: 3, minRelevanceScore: 0.77);

                string FUNCTION_DEFINITION = $"Act as the company {document.Publisher}. Answer questions about your annual financial report. Only answer questions based on the info listed below. If the info below doesn't answer the question, say you don't know.\n[START INFO]\n";

                await foreach (MemoryQueryResult r in results)
                {
                    int id = int.Parse(r.Metadata.Id);
                    MemoryQueryResult rb2 = kernel.Memory.GetAsync(memoryCollectionName, (id - 2).ToString()).Result;
                    MemoryQueryResult rb = kernel.Memory.GetAsync(memoryCollectionName, (id - 1).ToString()).Result;
                    MemoryQueryResult ra = kernel.Memory.GetAsync(memoryCollectionName, (id + 1).ToString()).Result;
                    MemoryQueryResult ra2 = kernel.Memory.GetAsync(memoryCollectionName, (id + 2).ToString()).Result;

                    FUNCTION_DEFINITION += "\n " + rb2.Metadata.Id + ": " + rb2.Metadata.Description + "\n";
                    FUNCTION_DEFINITION += "\n " + rb.Metadata.Description + "\n";
                    FUNCTION_DEFINITION += "\n " + r.Metadata.Description + "\n";
                    FUNCTION_DEFINITION += "\n " + ra.Metadata.Description + "\n";
                    FUNCTION_DEFINITION += "\n " + ra2.Metadata.Id + ": " + ra2.Metadata.Description + "\n";
                }

                FUNCTION_DEFINITION += "[END INFO]\n" + query;

                Debug.WriteLine(FUNCTION_DEFINITION + "\n\n");
                Debug.WriteLine(FUNCTION_DEFINITION.Length);

                var answer = kernel.CreateSemanticFunction(FUNCTION_DEFINITION, maxTokens: 250, temperature: 0);

                var result = await answer.InvokeAsync();
                Console.WriteLine($"\n{document.Path}: {result.Result.Trim()} \n");
            }
        }

        private static Catalog.Document GetDocumentSelection(Filings filings, string selection)
        {
            Catalog.Document document = null;
            do
            {
                Console.WriteLine($"Select a document from {string.Join(", ", filings.Documents.Select(x => x.Path))}");
                selection = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(selection))
                {
                    document = filings.Documents.FirstOrDefault(x => x.Path.ToLowerInvariant() == selection.ToLowerInvariant());
                }
            } while (document == null);
            return document;
        }
    }
}