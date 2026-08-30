namespace SimurghDashboard.Clock.Models
{
    // Represents the immutable data model for the weather state.
    // Note: UI-specific properties like TemperatureVisibility, ConditionTextVisibility, etc., 
    // are intentionally omitted. View-layer concerns should be handled in XAML using 
    // BooleanToVisibilityConverter or specific ViewModels, keeping this model pure.
    public sealed record WeatherState
    {
        // The current temperature (e.g., "24°C").
        public string Temperature { get; init; } = "--";

        // The text description of the current weather condition (e.g., "Sunny", "Partly Cloudy").
        public string ConditionText { get; init; } = "Unknown";

        // The Unicode character, font glyph, or icon key representing the weather condition.
        public string ConditionIcon { get; init; } = "\u2601";

        // The current humidity percentage (e.g., "45%").
        public string Humidity { get; init; } = "--%";

        // The current wind speed and direction (e.g., "15 km/h NW").
        public string Wind { get; init; } = "-- km/h";

        // Indicates if the background service is actively fetching data.
        public bool IsLoading { get; init; }

        // Indicates if the last fetch attempt failed or the service is in a faulted state.
        public bool HasError { get; init; }

        // Tracks when the weather data was last successfully updated.
        public DateTime LastUpdated { get; init; }
    }
}
