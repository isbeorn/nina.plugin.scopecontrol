using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ScopeControl {
    [Export(typeof(IDockableVM))]
    public class AltitudeDock : DockableVM, ITelescopeConsumer {
        private INighttimeCalculator nighttimeCalculator;
        private ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public AltitudeDock(
            IProfileService profileService, 
            ITelescopeMediator telescopeMediator,
            INighttimeCalculator nighttimeCalculator) : base(profileService) {

            var dict = new ResourceDictionary();
            dict.Source = new Uri("ScopeControl;component/DataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ScopeControl_AltitudeSVG"];
            ImageGeometry.Freeze();

            Task.Run(() => {
                NighttimeData = nighttimeCalculator.Calculate();
                nighttimeCalculator.OnReferenceDayChanged += NighttimeCalculator_OnReferenceDayChanged;
            });
            this.nighttimeCalculator = nighttimeCalculator;
            this.telescopeMediator = telescopeMediator;
            telescopeMediator.RegisterConsumer(this);
            Title = "Altitude Chart";
            Target = null;

            profileService.LocationChanged += (object sender, EventArgs e) => {
                Target?.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                Target?.SetCustomHorizon(profileService.ActiveProfile.AstrometrySettings.Horizon);
            };
        }

        private void NighttimeCalculator_OnReferenceDayChanged(object sender, EventArgs e) {
            NighttimeData = nighttimeCalculator.Calculate();
            RaisePropertyChanged(nameof(NighttimeData));
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }
        public NighttimeData NighttimeData { get; private set; }
        public TelescopeInfo TelescopeInfo { get; private set; }
        public DeepSkyObject Target { get; private set; }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            if(IsVisible) { 
                TelescopeInfo = deviceInfo;
                if(TelescopeInfo.Connected && TelescopeInfo.TrackingEnabled && NighttimeData != null) {
                    var showMoon = Target != null ? Target.Moon.DisplayMoon : false;
                    if(Target == null || (Target?.Coordinates - deviceInfo.Coordinates)?.Distance.Degree > 0.01) { 
                        Target = new DeepSkyObject("", deviceInfo.Coordinates, "", profileService.ActiveProfile.AstrometrySettings.Horizon);
                        Target.SetDateAndPosition(NighttimeCalculator.GetReferenceDate(DateTime.Now), profileService.ActiveProfile.AstrometrySettings.Latitude, profileService.ActiveProfile.AstrometrySettings.Longitude);
                        if(showMoon) {
                            Target.Refresh();
                            Target.Moon.DisplayMoon = true;
                        }
                        RaisePropertyChanged(nameof(Target));
                    }
                } else {
                    Target = null;
                    RaisePropertyChanged(nameof(Target));
                }
                RaisePropertyChanged(nameof(TelescopeInfo));
            }
        }
    }
}
