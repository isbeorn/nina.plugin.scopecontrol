using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ScopeControl {
    [Export(typeof(IPluginManifest))]
    public class ScopeControlPlugin : PluginBase, INotifyPropertyChanged {
        [ImportingConstructor]
        public ScopeControlPlugin(IProfileService profileService) {
            this.PluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            ScopeControlMediator.Instance.RegisterPlugin(this);
        }

        public PluginOptionsAccessor PluginSettings { get; }

        public AzimuthDisplay AzimuthDisplay {
            get {
                return (AzimuthDisplay)PluginSettings.GetValueInt32(nameof(AzimuthDisplay), 0);
            }
            set {
                PluginSettings.SetValueInt32(nameof(AzimuthDisplay), (int)value);
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ScopeControlMediator {

        private ScopeControlMediator() { }

        private static readonly Lazy<ScopeControlMediator> lazy = new Lazy<ScopeControlMediator>(() => new ScopeControlMediator());

        public static ScopeControlMediator Instance { get => lazy.Value; }
        public void RegisterPlugin(ScopeControlPlugin toolsPlugin) {
            this.ScopeControlPlugin = toolsPlugin;
        }

        public ScopeControlPlugin ScopeControlPlugin { get; private set; }

    }
}
