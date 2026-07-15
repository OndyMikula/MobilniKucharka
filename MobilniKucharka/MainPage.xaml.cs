using MobilniKucharka.Classes;
using System.Text.Json;

namespace MobilniKucharka
{
    public partial class MainPage : ContentPage
    {
        private List<Recipe> _allRecipes = new();
        private readonly DatabaseService _databaseService = new DatabaseService();
        private static bool _isInitStarted = false;

        // Nástroj pro stahování z internetu
        private static readonly HttpClient _httpClient = new HttpClient();

        public MainPage()
        {
            InitializeComponent();
            LoadData(); // Načte základní kategorie, suroviny a lokální mock recepty
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_isInitStarted)
            {
                _isInitStarted = true;
                // 1. Nejprve vyřešíme SQLite databázi (import z CSV proběhne jen při úplně prvním spuštění)
                await InitializeDatabaseIfNeededAsync();
                // 2. Teprve až je stránka vykreslená a DB připravená, bezpečně stáhneme recepty z internetu
                //_ = FetchRecipesOnlineAsync();
            }
        }

        private async Task InitializeDatabaseIfNeededAsync()
        {
            try
            {
                // Zkontrolujeme, zda jsme už CSV někdy v minulosti naimportovali
                bool isDbImported = Preferences.Default.Get("IsDatabaseImported", false);

                if (!isDbImported)
                {
                    // Zobrazíme uživateli jednoduché info, že se připravují data
                    await DisplayAlert("První spuštění", "Chvíli strpení, připravujeme databázi potravin...", "OK");

                    // Načteme soubor z instalačního balíčku aplikace
                    using var stream = await FileSystem.OpenAppPackageFileAsync("nutridatabaze.csv");

                    // Spustíme náš import z DatabaseService
                    await _databaseService.ImportCsvDatabaseAsync(stream);

                    // Uložíme příznak, že je hotovo – příště už se tento blok přeskočí
                    Preferences.Default.Set("IsDatabaseImported", true);

                    await DisplayAlert("Hotovo", "Databáze potravin byla úspěšně připravena offline.", "Super");
                }
            }
            catch (Exception ex)
            {
                // Ošetření chyb (např. chybějící soubor nebo špatný formát)
                await DisplayAlert("Chyba", $"Nepodařilo se inicializovat databázi: {ex.Message}", "OK");
            }
        }

        private void LoadData()
        {
            // 1. Naplnění kategorií (Živiny)
            CategoriesList.ItemsSource = new List<FilterItem>
            {
                new FilterItem { Name = "Bílkoviny" },
                new FilterItem { Name = "Sacharidy" },
                new FilterItem { Name = "Zelenina" },
                new FilterItem { Name = "Ovoce" },
                new FilterItem { Name = "Zdravé tuky" },
                new FilterItem { Name = "Mastný jídlo" }
            };

            // 2. Naplnění surovin (Co mám doma)
            IngredientsList.ItemsSource = new List<FilterItem>
            {
                new FilterItem { Name = "Maso" },
                new FilterItem { Name = "Rýže" },
                new FilterItem { Name = "Těstoviny" },
                new FilterItem { Name = "Vajíčka" },
                new FilterItem { Name = "Brambory" }
            };

            #region Recepty
            // 3. Mock databáze receptů
            _allRecipes = new List<Recipe>
            {
                new Recipe
                {
                    Name = "Kuřecí prsa s rýží",
                    ImageUrl = "https://images.unsplash.com/photo-1598514982205-f36b96d1e8d4",
                    Protein = 45, Carbs = 50, Fat = 5, Sugar = 2,
                    Tags = new List<string> { "Bílkoviny", "Sacharidy", "Maso", "Rýže" },
                    Tools = new List<string> { "Pánev", "Hrnec na rýži", "Prkénko", "Nůž" },
                    Steps = new List<string>
                    {
                        "Nakrájej maso na kostky a okořeň podle chuti.",
                        "Dej vařit rýži do hrnce s osolenou vodou (15 minut).",
                        "Orestuj maso na pánvi dozlatova.",
                        "Smíchej a servíruj s kouskem čerstvé zeleniny."
                    },
                    SourceUrl = "https://www.toprecepty.cz"
                },
                new Recipe
                {
                    Name = "Vaječná omeleta se zeleninou",
                    ImageUrl = "https://images.unsplash.com/photo-1510693206972-df098062cb71",
                    Protein = 20, Carbs = 2, Fat = 15, Sugar = 1,
                    Tags = new List<string> { "Bílkoviny", "Zdravé tuky", "Vajíčka", "Zelenina" },
                    Tools = new List<string> { "Pánev", "Miska", "Metlička", "Obracečka" },
                    Steps = new List<string>
                    {
                        "Rozklepni 3 vajíčka do misky, přidej sůl, pepř a pořádně je rozšlehej metličkou.",
                        "Na kapce olivového oleje zlehka orestuj nakrájenou zeleninu.",
                        "Zalij zeleninu vajíčky a nech na mírném ohni opékat zhruba 3 až 5 minut.",
                        "Opatrně přehni napůl a můžeš podávat."
                    }
                },
                new Recipe
                {
                    Name = "Těstovinový salát s tuňákem",
                    ImageUrl = "https://images.unsplash.com/photo-1608897013039-887f21d8c804",
                    Protein = 25, Carbs = 60, Fat = 12, Sugar = 4,
                    Tags = new List<string> { "Sacharidy", "Bílkoviny", "Těstoviny", "Maso" },
                    Tools = new List<string> { "Velký hrnec", "Sítko", "Mísa na salát" },
                    Steps = new List<string>
                    {
                        "Uvař těstoviny podle návodu na obalu.",
                        "Sceď těstoviny a nech je chvíli vychladnout.",
                        "V misce smíchej těstoviny s tuňákem z konzervy a přidej lžíci jogurtu nebo majonézy.",
                        "Dochuť solí a pepřem."
                    }
                },
                new Recipe
                {
                    Name = "Pečený losos s bramborem",
                    ImageUrl = "https://images.unsplash.com/photo-1467003909585-2f8a72700288",
                    Protein = 35, Carbs = 40, Fat = 22, Sugar = 2,
                    Tags = new List<string> { "Bílkoviny", "Zdravé tuky", "Maso", "Brambory" },
                    Tools = new List<string> { "Plech na pečení", "Pečicí papír", "Nůž" },
                    Steps = new List<string>
                    {
                        "Předehřej troubu na 200 °C.",
                        "Brambory nakrájej na měsíčky, dej na plech, zakápni olejem a osol.",
                        "Peč brambory 20 minut, pak k nim přidej osolený filet z lososa kůží dolů.",
                        "Peč dalších 15 minut, dokud není losos hotový."
                    }
                },
                new Recipe
                {
                    Name = "Avokádový toust s vejcem",
                    ImageUrl = "https://images.unsplash.com/photo-1525351484163-7529414344d8",
                    Protein = 15, Carbs = 30, Fat = 20, Sugar = 3,
                    Tags = new List<string> { "Sacharidy", "Zdravé tuky", "Vajíčka", "Zelenina" },
                    Tools = new List<string> { "Topinkovač", "Malý kastrůlek", "Vidlička" },
                    Steps = new List<string>
                    {
                        "Dej opéct plátky pečiva.",
                        "Vydlabej avokádo a rozmačkej ho vidličkou se solí, pepřem a kapkou citronu.",
                        "Uvař vejce natvrdo nebo nahniličko (cca 6-7 minut).",
                        "Namaž avokádo na toust a nahoru polož nakrájené vajíčko."
                    }
                }
            };
            #endregion

            // Na začátku zobrazíme všechny
            RecipesList.ItemsSource = _allRecipes;
        }

        private async Task FetchRecipesOnlineAsync()
        {
            try
            {
                // Otevřeme reálnou online databázi (TheMealDB má veřejné API)
                string url = "https://www.themealdb.com/api/json/v1/1/search.php?s=";
                string jsonResponse = await _httpClient.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;
                JsonElement meals = root.GetProperty("meals");

                List<Recipe> downloadedRecipes = new();

                // Projdeme stažené recepty z netu a převedeme je na náš formát
                foreach (JsonElement meal in meals.EnumerateArray())
                {
                    // Bezpečné ošetření konců řádků (\r\n i \n)
                    string instructions = meal.GetProperty("strInstructions").GetString() ?? string.Empty;
                    List<string> steps = instructions
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    downloadedRecipes.Add(new Recipe
                    {
                        Name = meal.GetProperty("strMeal").GetString() ?? "Bez názvu",
                        ImageUrl = meal.GetProperty("strMealThumb").GetString(),
                        SourceUrl = meal.GetProperty("strSource").ValueKind != JsonValueKind.Null
                            ? meal.GetProperty("strSource").GetString()
                            : meal.GetProperty("strYoutube").GetString(),
                        Steps = steps,

                        // BEZPEČNOSTNÍ FIX: Inicializace prázdných seznamů, aby vyhledávání neházelo NullReferenceException
                        Tags = new List<string>(),
                        Tools = new List<string>()
                    });
                }

                _allRecipes = downloadedRecipes;

                // UI aktualizace provádíme na hlavním vlákně zařízení
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RecipesList.ItemsSource = _allRecipes;
                });
            }
            catch (Exception)
            {
                // Pokud selže síť, tiše zachováme lokální mock recepty, 
                // ať uživatele neotravujeme chybou hned po startu.
            }
        }

        private void OnSearchClicked(object sender, EventArgs e)
        {
            // Získání vybraných tagů z obou seznamů
            var selectedCategories = CategoriesList.SelectedItems.Cast<FilterItem>().Select(x => x.Name).ToList();
            var selectedIngredients = IngredientsList.SelectedItems.Cast<FilterItem>().Select(x => x.Name).ToList();

            var allSelectedTags = selectedCategories.Concat(selectedIngredients).ToList();

            if (allSelectedTags.Count == 0)
            {
                RecipesList.ItemsSource = _allRecipes; // Pokud nic nevybral, ukaž vše
                return;
            }

            // Filtrování receptů: Hledáme takové, které obsahují alespoň jeden vybraný tag
            // Doplněno bezpečné ověření r.Tags != null
            var filtered = _allRecipes
                .Where(r => r.Tags != null && r.Tags.Intersect(allSelectedTags).Any())
                .ToList();

            RecipesList.ItemsSource = filtered;
        }

        private async void OnRecipeSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Recipe selectedRecipe)
            {
                // Otevření detailu
                await Navigation.PushAsync(new RecipeDetailPage(selectedRecipe));

                // Odznačení po kliknutí
                ((CollectionView)sender).SelectedItem = null;
            }
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingsPage());
        }
    }
}