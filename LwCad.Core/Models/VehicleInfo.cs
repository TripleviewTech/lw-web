using LwCadSystemUX.ViewModels;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// Vehicle block behind "Add Vehicle Info" on the Take Call panel.
    /// Mirrors the legacy tbxAV_Veh* / DSV1 fields.
    /// </summary>
    public class VehicleInfo : ObservableObject
    {
        private string _plate = string.Empty;
        private string _plateState = "TX";
        private string _plateType = string.Empty;
        private string _year = string.Empty;
        private string _make = string.Empty;
        private string _model = string.Empty;
        private string _style = string.Empty;
        private string _color = string.Empty;

        public string Plate { get => _plate; set => Set(ref _plate, value); }
        public string PlateState { get => _plateState; set => Set(ref _plateState, value); }
        public string PlateType { get => _plateType; set => Set(ref _plateType, value); }
        public string Year { get => _year; set => Set(ref _year, value); }
        public string Make { get => _make; set => Set(ref _make, value); }
        public string Model { get => _model; set => Set(ref _model, value); }
        public string Style { get => _style; set => Set(ref _style, value); }
        public string Color { get => _color; set => Set(ref _color, value); }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Plate) &&
            string.IsNullOrWhiteSpace(Make) &&
            string.IsNullOrWhiteSpace(Model) &&
            string.IsNullOrWhiteSpace(Style) &&
            string.IsNullOrWhiteSpace(Color) &&
            string.IsNullOrWhiteSpace(Year);

        public string Summary
        {
            get
            {
                if (IsEmpty) return string.Empty;
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(Year)) parts.Add(Year.Trim());
                if (!string.IsNullOrWhiteSpace(Color)) parts.Add(Color.Trim());
                if (!string.IsNullOrWhiteSpace(Make)) parts.Add(Make.Trim());
                if (!string.IsNullOrWhiteSpace(Model)) parts.Add(Model.Trim());
                if (!string.IsNullOrWhiteSpace(Plate)) parts.Add("[" + Plate.Trim() + " " + PlateState + "]");
                return string.Join(" ", parts);
            }
        }
    }
}
