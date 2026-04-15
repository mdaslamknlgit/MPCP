using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MPC.PlanSched.UI.Services
{
    /// <summary>
    /// The class that stores the user settings
    /// </summary>
    public class UserSettings : INotifyPropertyChanged
    {
        private string _planType = string.Empty;
        public string PlanType
        {
            get => _planType;
            set
            {
                _planType = value;
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
