using SQLite;
using System.Text.Json;

namespace MobilniKucharka.Classes.Recipe
{
    public class Recipe
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Podpora pro oba jazyky přímo v databázi (Dual-Language)
        public string Name_CS { get; set; } = string.Empty;
        public string Name_EN { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }

        public string SourceUrl { get; set; } = string.Empty;

        // SQLite tyto reálné Listy ignoruje
        [Ignore]
        public List<string> Steps_CS { get; set; } = [];
        [Ignore]
        public List<string> Steps_EN { get; set; } = [];
        [Ignore]
        public List<string> Tags { get; set; } = [];
        [Ignore]
        public List<string> Tools { get; set; } = [];

        // Tyto vlastnosti ukládá SQLite jako text (JSON) do databáze
        public string StepsCsDb
        {
            get => JsonSerializer.Serialize(Steps_CS);
            set => Steps_CS = string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }

        public string StepsEnDb
        {
            get => JsonSerializer.Serialize(Steps_EN);
            set => Steps_EN = string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }

        public string TagsDb
        {
            get => JsonSerializer.Serialize(Tags);
            set => Tags = string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }

        public string ToolsDb
        {
            get => JsonSerializer.Serialize(Tools);
            set => Tools = string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
    }
}