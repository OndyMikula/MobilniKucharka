using SQLite;
using System.Text.Json;

namespace MobilniKucharka.Classes.Recipe
{
    public class Recipe
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name_CS { get; set; } = string.Empty;
        public string Name_EN { get; set; } = string.Empty;

        // Nutriční hodnoty
        public int PrepTime { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }
        public double Sugar { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        //User actions
        public double ManualCost { get; set; }
        public double Rating { get; set; } = 0.0; // Podpora pro půlhvězdičky (např. 3.5)
        public bool IsDraft { get; set; } = false; // TRUE = Rozepsaný recept (pro auto-save)
        public int? BookmarkId { get; set; } // Do které záložky patří (null = do žádné, "Oblíbené" bude mít pevné ID)
        public string Category { get; set; } = string.Empty; // Kategorie receptu (např. "Vytvořené recepty", "Oblíbené", "Snídaně", "Večeře" apod.)

        //User created recipe data
        public string DescriptionText { get; set; } = string.Empty; // Popis receptu (např. "Tento recept je rychlý a jednoduchý.")
        public bool IsNutritionEstimated { get; set; } // TRUE = Nutriční hodnoty jsou odhadnuté, FALSE = Nutriční hodnoty jsou přesné (např. z API)
        public string IngredientsRaw { get; set; } = string.Empty; // Suroviny (např. "1 vejce|1 ks\nMouka|200 g")
        public string StepsRaw { get; set; } = string.Empty; // Postup (např. "1. Smíchejte ingredience.\n2. Pečte 20 minut.")

        // UKLÁDÁNÍ DO DB: JSON řetězce pro češtinu i angličtinu
        public string StepsJson_CS { get; set; } = "[]";
        public string StepsJson_EN { get; set; } = "[]";
        public string StepsJson { get; set; } = string.Empty;

        public string EquipmentJson { get; set; } = "[]";
        public string DietaryFlagsJson { get; set; } = "[]";

        public string ExternalSourceId { get; set; } = string.Empty;

        // PRO PRÁCI V KÓDU: Automatická serializace/deserializace
        [Ignore]
        public string Name
        {
            get
            {
                // Přečteme aktuální jazyk (výchozí je čeština)
                string currentLang = Preferences.Default.Get("AppLanguageCode", "cs");
                return currentLang == "cs" ? Name_CS : Name_EN;
            }
        }

        [Ignore]
        public bool HasPrepTime => PrepTime > 0;

        private List<string>? _stepsCs;
        [Ignore]
        public List<string> Steps_CS
        {
            get
            {
                _stepsCs ??= string.IsNullOrEmpty(StepsJson_CS) ? [] : JsonSerializer.Deserialize<List<string>>(StepsJson_CS) ?? [];
                return _stepsCs;
            }
            set
            {
                _stepsCs = value;
                StepsJson_CS = JsonSerializer.Serialize(value);
            }
        }

        private List<string>? _stepsEn;
        [Ignore]
        public List<string> Steps_EN
        {
            get
            {
                _stepsEn ??= string.IsNullOrEmpty(StepsJson_EN) ? [] : JsonSerializer.Deserialize<List<string>>(StepsJson_EN) ?? [];
                return _stepsEn;
            }
            set
            {
                _stepsEn = value;
                StepsJson_EN = JsonSerializer.Serialize(value);
            }
        }

        private List<string>? _equipment;
        [Ignore]
        public List<string> Equipment
        {
            get
            {
                _equipment ??= string.IsNullOrEmpty(EquipmentJson) ? [] : JsonSerializer.Deserialize<List<string>>(EquipmentJson) ?? [];
                return _equipment;
            }
            set
            {
                _equipment = value;
                EquipmentJson = JsonSerializer.Serialize(value);
            }
        }

        private List<string>? _dietaryFlags;
        [Ignore]
        public List<string> DietaryFlags
        {
            get
            {
                _dietaryFlags ??= string.IsNullOrEmpty(DietaryFlagsJson) ? [] : JsonSerializer.Deserialize<List<string>>(DietaryFlagsJson) ?? [];
                return _dietaryFlags;
            }
            set
            {
                _dietaryFlags = value;
                DietaryFlagsJson = JsonSerializer.Serialize(value);
            }
        }
    }
}
