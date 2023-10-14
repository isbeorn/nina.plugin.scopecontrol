using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace ScopeControl {

    [Export(typeof(IDockableVM))]
    public class AzimuthDock : DockableVM, ITelescopeConsumer {
        private ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public AzimuthDock(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator) : base(profileService) {
            var dict = new ResourceDictionary();
            dict.Source = new Uri("ScopeControl;component/DataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ScopeControl_AzimuthSVG"];
            ImageGeometry.Freeze();

            this.telescopeMediator = telescopeMediator;
            telescopeMediator.RegisterConsumer(this);
            Title = "Azimuth Chart";

            Horizon = new List<HorizonPoint>();
            TelescopePath = new List<DataPoint>();

            profileService.LocationChanged += (object sender, EventArgs e) => {
                currentTelescopeCoordinates = null;
                UpdatePole();
            };

            profileService.HorizonChanged += (object sender, EventArgs e) => {
                BuildHorizon();
            };
            profileService.ProfileChanged += (object sender, EventArgs e) => {
                currentTelescopeCoordinates = null;
                UpdatePole();
            };
            var timer = new System.Timers.Timer(TimeSpan.FromMinutes(5));
            timer.Elapsed += UpdateMoonPosition;
            timer.Enabled = true;

            BuildHorizon();
            BuildMoonData();
            UpdateMoonPosition(null, null);
            UpdatePole();

            var quarter = (System.Windows.Media.GeometryGroup)System.Windows.Application.Current.Resources["FirstQuarterMoonSVG"];
            
        }

        private void UpdatePole() {
            var northern = profileService.ActiveProfile.AstrometrySettings.Latitude > 0;
            var alt = northern ? -profileService.ActiveProfile.AstrometrySettings.Latitude : profileService.ActiveProfile.AstrometrySettings.Latitude;
            var az = northern ? 0 : 180;
            PolePosition = new DataPoint(alt, az);
        }

        private DateTime currentReferenceDate;

        private void UpdateMoonPosition(object sender, System.Timers.ElapsedEventArgs e) {
            var newReferencedate = NighttimeCalculator.GetReferenceDate(DateTime.Now);
            if(newReferencedate != currentReferenceDate) {
                // Day switch
                BuildMoonData();
            }

            var date = DateTime.Now;
            var jd = AstroUtil.GetJulianDate(date);
            var siderealTime = AstroUtil.GetLocalSiderealTime(date, profileService.ActiveProfile.AstrometrySettings.Longitude);
            var tuple = AstroUtil.GetMoonAndSunPosition(date, jd, new ObserverInfo() {  Latitude = profileService.ActiveProfile.AstrometrySettings.Latitude, Longitude = profileService.ActiveProfile.AstrometrySettings.Longitude });

            var ha = AstroUtil.GetHourAngle(siderealTime, tuple.Item1.RA);

            var alt = AstroUtil.GetAltitude(Angle.ByHours(ha), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(tuple.Item1.Dec));
            var az = AstroUtil.GetAzimuth(Angle.ByHours(ha), alt, Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(tuple.Item1.Dec));
            MoonPosition = new DataPoint(-alt.Degree, az.Degree);
        }


        private void BuildHorizon() {
            var h = new List<HorizonPoint>();
            CustomHorizon customHorizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
            if (customHorizon != null) {
                for (int azimuth = 0; azimuth <= 360; azimuth++) {
                    var horizonAltitude = customHorizon.GetAltitude(azimuth);
                    h.Add(new HorizonPoint(-horizonAltitude, azimuth));
                }
            } else {
                for (int azimuth = 0; azimuth <= 360; azimuth++) {
                    h.Add(new HorizonPoint(0, azimuth));
                }
            }
            Horizon = h;
        }

        private void BuildMoonData() {
            var m = new List<DataPoint>();
            var startDate = DateTime.Now;
            currentReferenceDate = startDate;
            var date = startDate;
            for (double timeIncr = 0; timeIncr < 24; timeIncr += 0.1) {
                var jd = AstroUtil.GetJulianDate(date);
                var tuple = AstroUtil.GetMoonAndSunPosition(date, jd);

                var siderealTime = AstroUtil.GetLocalSiderealTime(date, profileService.ActiveProfile.AstrometrySettings.Longitude);
                var ha = AstroUtil.GetHourAngle(siderealTime, tuple.Item1.RA);

                var alt = AstroUtil.GetAltitude(Angle.ByHours(ha), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(tuple.Item1.Dec));
                var az = AstroUtil.GetAzimuth(Angle.ByHours(ha), alt, Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(tuple.Item1.Dec));
                if (alt.Degree > 0) {
                    m.Add(new DataPoint(-alt.Degree, az.Degree));
                } else {
                    m.Add(DataPoint.Undefined);
                }
                date = startDate + TimeSpan.FromHours(timeIncr);
            }
            Moon = m;            
            MoonPhase = AstroUtil.GetMoonPhase(currentReferenceDate);
        }

        private AstroUtil.MoonPhase moonPhase;
        public AstroUtil.MoonPhase MoonPhase {
            get => moonPhase;
            set {
                moonPhase = value;
                RaisePropertyChanged();
            }
        }
        private bool showMoon;
        public bool ShowMoon { 
            get => showMoon; 
            set { 
                showMoon = value;
                if(value) {
                    var info = new MoonInfo(null);
                    var color = info.Color;
                    color.A = 100;
                    MoonColor = color;
                } else {
                    MoonColor = Colors.Transparent;
                }
                
                RaisePropertyChanged(); 
            } 
        }

        private string moonSeparation;
        public string MoonSeparation {
            get => moonSeparation;
            set {
                moonSeparation = value;
                RaisePropertyChanged();
            }
        }

        private Color moonColor;
        public Color MoonColor {
            get => moonColor;
            set {
                moonColor = value;
                RaisePropertyChanged();
            }
        }

        private List<DataPoint> moon;

        public List<DataPoint> Moon {
            get {
                return moon;
            }
            private set {
                moon = value;
                RaisePropertyChanged();
            }
        }

        private List<HorizonPoint> horizon;

        public List<HorizonPoint> Horizon {
            get {
                return horizon;
            }
            private set {
                horizon = value;
                RaisePropertyChanged();
            }
        }

        private List<DataPoint> telescopePath;

        public List<DataPoint> TelescopePath {
            get {
                return telescopePath;
            }
            private set {
                telescopePath = value;
                RaisePropertyChanged();
            }
        }

        private Coordinates currentTelescopeCoordinates;
        private DataPoint telescopePosition;
        public DataPoint TelescopePosition {
            get => telescopePosition;
            set {
                if(telescopePosition.X != value.X || telescopePosition.Y != value.Y) {
                    telescopePosition = value;
                    RaisePropertyChanged();
                }                
            }
        }

        private DataPoint moonPosition;
        public DataPoint MoonPosition {
            get => moonPosition;
            set {
                if (moonPosition.X != value.X || moonPosition.Y != value.Y) {
                    moonPosition = value;
                    RaisePropertyChanged();
                }
            }
        }

        private DataPoint polePosition;
        public DataPoint PolePosition {
            get => polePosition;
            set {
                if (polePosition.X != value.X || polePosition.Y != value.Y) {
                    polePosition = value;
                    RaisePropertyChanged();
                }
            }
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            if (IsVisible) {
                try {
                    if (deviceInfo.Connected) {
                        if (deviceInfo.TrackingEnabled && deviceInfo.TrackingRate.TrackingMode == TrackingMode.Sidereal) {
                            if (currentTelescopeCoordinates == null || (currentTelescopeCoordinates - deviceInfo.Coordinates).Distance.Degree > 1) {
                                currentTelescopeCoordinates = deviceInfo.Coordinates;
                                var path = new List<DataPoint>();

                                var latitude = profileService.ActiveProfile.AstrometrySettings.Latitude;
                                var longitude = profileService.ActiveProfile.AstrometrySettings.Longitude;

                                var siderealTime = AstroUtil.GetLocalSiderealTimeNow(longitude);
                                var start = currentTelescopeCoordinates.RA;

                                for (double angle = 0; angle < 24; angle += 0.1) {

                                    var ha = AstroUtil.GetHourAngle(siderealTime, start - angle);
                                    var alt = AstroUtil.GetAltitude(Angle.ByHours(ha), Angle.ByDegree(latitude), Angle.ByDegree(currentTelescopeCoordinates.Dec));
                                    var az = AstroUtil.GetAzimuth(Angle.ByHours(ha), alt, Angle.ByDegree(latitude), Angle.ByDegree(currentTelescopeCoordinates.Dec));
                                    if (alt.Degree > 0) {
                                      path.Add(new DataPoint(-alt.Degree, az.Degree));
                                    } else {
                                        path.Add(DataPoint.Undefined);
                                    }
                                }

                                TelescopePath = path;
                                var mi = new MoonInfo(currentTelescopeCoordinates);
                                mi.SetReferenceDateAndObserver(currentReferenceDate, new ObserverInfo() { Latitude = latitude, Longitude = longitude });
                                MoonSeparation = mi.SeparationText;
                            }
                        } else {
                            // Not tracking - clear path and distance
                            TelescopePath = new List<DataPoint>();
                            MoonSeparation = "--";
                        }
                        TelescopePosition = new DataPoint(-deviceInfo.Altitude, deviceInfo.Azimuth);
                    } else {
                        currentTelescopeCoordinates = null;
                        TelescopePosition = new DataPoint(0, 0);
                        TelescopePath = new List<DataPoint>();
                        MoonSeparation = "--";
                    }
                } catch { }
            } 
        }
    }

    public class HorizonPoint {
        public HorizonPoint(double x, double y) {
            this.X = x;
            this.Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public double Y2 => 0;
        public double X2 => 0;
    }
}
