using Microsoft.Maui.Controls.Shapes;

namespace MobilniKucharka.Classes
{
    public static class StarRatingHelper
    {
        // Statické zobrazení (jen pro čtení) - používá se v RecipeDetailPage
        public static void Render(Layout container, double rating, double starSize = 24, Color? filledColor = null, Color? emptyColor = null)
        {
            var (filled, empty) = ResolveColors(filledColor, emptyColor);

            container.Children.Clear();
            for (int i = 0; i < 5; i++)
            {
                container.Children.Add(BuildStarVisual(i, rating, starSize, filled, empty));
            }
        }

        // Interaktivní verze - klepnutím na levou/pravou polovinu hvězdy nastavíš půl/celou hvězdu
        public static void RenderInteractive(Layout container, double initialRating, Action<double> onRatingChanged, double starSize = 34, Color? filledColor = null, Color? emptyColor = null)
        {
            var (filled, empty) = ResolveColors(filledColor, emptyColor);
            double currentRating = initialRating;

            container.Children.Clear();

            for (int i = 0; i < 5; i++)
            {
                int starIndex = i;
                var wrapper = new Grid { WidthRequest = starSize, HeightRequest = starSize };
                wrapper.Children.Add(BuildStarVisual(starIndex, currentRating, starSize, filled, empty));

                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) =>
                {
                    var position = e.GetPosition(wrapper);
                    bool tappedLeftHalf = position.HasValue && position.Value.X < starSize / 2;
                    currentRating = starIndex + (tappedLeftHalf ? 0.5 : 1.0);

                    RenderInteractive(container, currentRating, onRatingChanged, starSize, filledColor, emptyColor);
                    onRatingChanged(currentRating);
                };
                wrapper.GestureRecognizers.Add(tap);

                container.Children.Add(wrapper);
            }
        }

        private static (Color filled, Color empty) ResolveColors(Color? filledColor, Color? emptyColor)
        {
            return (filledColor ?? Color.FromArgb("#FFC107"), emptyColor ?? Color.FromArgb("#E0E0E0"));
        }

        private static View BuildStarVisual(int index, double rating, double starSize, Color filledColor, Color emptyColor)
        {
            int fullStars = (int)Math.Floor(rating);
            double remainder = rating - fullStars;
            bool hasHalfStar = remainder >= 0.25 && remainder < 0.75;
            if (remainder >= 0.75) fullStars++;

            if (index < fullStars)
                return new Label { Text = "★", FontSize = starSize, TextColor = filledColor, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };

            if (index == fullStars && hasHalfStar)
                return BuildHalfStar(starSize, filledColor, emptyColor);

            return new Label { Text = "★", FontSize = starSize, TextColor = emptyColor, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        }

        private static Grid BuildHalfStar(double starSize, Color filledColor, Color emptyColor)
        {
            var grid = new Grid { WidthRequest = starSize, HeightRequest = starSize };

            grid.Children.Add(new Label { Text = "★", FontSize = starSize, TextColor = emptyColor });
            grid.Children.Add(new Label
            {
                Text = "★",
                FontSize = starSize,
                TextColor = filledColor,
                Clip = new RectangleGeometry(new Rect(0, 0, starSize / 2, starSize))
            });

            return grid;
        }
    }
}