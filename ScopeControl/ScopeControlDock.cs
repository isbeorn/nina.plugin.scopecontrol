﻿using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Telescope;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ScopeControl {
    [Export(typeof(IDockableVM))]
    public class ScopeControlDock : DockableVM, ITelescopeConsumer {
        private ITelescopeMediator telescopeMediator;
        private IProgress<ApplicationStatus> progress;

        [ImportingConstructor]
        public ScopeControlDock(IProfileService profileService, ITelescopeMediator telescopeMediator, IGuiderMediator guiderMediator, IApplicationStatusMediator applicationStatusMediator) : base(profileService) {
            this.telescopeMediator = telescopeMediator;
            telescopeMediator.RegisterConsumer(this);

            Title = "Scope Control";
            AxisRates = new List<double>(); 
            
            var dict = new ResourceDictionary();
            dict.Source = new Uri("ScopeControl;component/DataTemplates.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["ScopeControlSVG"];
            ImageGeometry.Freeze();

            SlewScopeToRaDec = new SlewScopeToRaDec(telescopeMediator, guiderMediator);
            SlewScopeToAltAz = new SlewScopeToAltAz(profileService, telescopeMediator, guiderMediator);

            MoveAxisCommand = new RelayCommand(o => MoveAxis((Direction)Enum.Parse(typeof(Direction), o.ToString(), true), AxisRate));
            StopMoveAxisCommand = new RelayCommand(o => MoveAxis((Direction)Enum.Parse(typeof(Direction), o.ToString(), true), 0));

            StopCommand = new RelayCommand(o => {
                telescopeMediator.StopSlew();
                telescopeMediator.SetTrackingEnabled(false);
            });

            progress = new Progress<ApplicationStatus>(x => {
                x.Source = this.Title;
                applicationStatusMediator.StatusUpdate(x);
            });
            HomeCommand = new AsyncCommand<bool>(() => telescopeMediator.FindHome(progress, default));
            ParkCommand = new AsyncCommand<bool>(() => telescopeMediator.ParkTelescope(progress, default));
            UnparkCommand = new AsyncCommand<bool>(() => telescopeMediator.UnparkTelescope(progress, default));
            StartTrackingCommand = new RelayCommand((object o) => telescopeMediator.SetTrackingEnabled(true), (object o) => !TelescopeInfo.TrackingEnabled && !TelescopeInfo.AtPark);
            StopTrackingCommand = new RelayCommand((object o) => telescopeMediator.SetTrackingEnabled(false), (object o) => TelescopeInfo.TrackingEnabled && !TelescopeInfo.AtPark);
            
            SlewToAltAzCommand = new AsyncCommand<bool>(o => ItemRunner(SlewScopeToAltAz));
            SlewToRaDecCommand = new AsyncCommand<bool>(o => ItemRunner(SlewScopeToRaDec));

        }
        private async Task<bool> ItemRunner(ISequenceItem item) {

            item.ResetProgress();
            var isValid = (item as IValidatable)?.Validate() ?? true;

            if (!isValid) {
                Notification.ShowError(string.Join(Environment.NewLine, SlewScopeToRaDec.Issues));
                return false;
            }

            await item.Run(default, default);
            return true;
        }

        public override bool IsTool => true;

        public ICommand MoveAxisCommand { get; }
        public ICommand StopMoveAxisCommand { get; }
        public ICommand StopCommand { get; }
        public IAsyncCommand HomeCommand { get; }
        public IAsyncCommand ParkCommand { get; }
        public IAsyncCommand UnparkCommand { get; }
        public ICommand StartTrackingCommand { get; }
        public ICommand StopTrackingCommand { get; }
        
        public IAsyncCommand SlewToAltAzCommand { get; }
        public IAsyncCommand SlewToRaDecCommand { get; }

        private double axisRate;
        public double AxisRate {
            get => axisRate;
            set {
                axisRate = value;
                RaisePropertyChanged();
            }
        }

        public SlewScopeToAltAz SlewScopeToAltAz { get; }

        public SlewScopeToRaDec SlewScopeToRaDec { get; }
 

        public List<double> AxisRates { get; private set; }

        enum Direction {
            EAST,
            WEST,
            NORTH,
            SOUTH
        }

        private void MoveAxis(Direction direction, double rate) {
            switch(direction) {
                case Direction.EAST: 
                    telescopeMediator.MoveAxis(NINA.Core.Enum.TelescopeAxes.Primary, rate);
                    break;
                
                case Direction.WEST:
                    telescopeMediator.MoveAxis(NINA.Core.Enum.TelescopeAxes.Primary, -rate);
                    break;

                case Direction.NORTH:
                    telescopeMediator.MoveAxis(NINA.Core.Enum.TelescopeAxes.Secondary, rate);
                    break;

                case Direction.SOUTH:
                    telescopeMediator.MoveAxis(NINA.Core.Enum.TelescopeAxes.Secondary, -rate);
                    break;
            }            
        }

        public TelescopeInfo TelescopeInfo { get; private set; }
        private bool wasConnected = false;
        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            TelescopeInfo = deviceInfo;
            RaisePropertyChanged(nameof(TelescopeInfo));            
            if(TelescopeInfo.Connected) {
                if(!wasConnected) {
                    var availableRates = new List<double>();
                    foreach(var rate in TelescopeInfo.PrimaryAxisRates) {
                        var lower = (int)Math.Floor(rate.Item1);
                        var upper = (int)Math.Ceiling(rate.Item2);
                        if (rate.Item1 == rate.Item2) { 
                            availableRates.Add(rate.Item1);
                        } else {
                            for (int i = lower; i <= upper; i++) {
                                if (!availableRates.Contains(i)) {
                                    availableRates.Add(i);
                                }
                            }
                        }
                    }
                    AxisRates = availableRates;
                    RaisePropertyChanged(nameof(AxisRates));
                    if(AxisRates.Count > 0) {
                        AxisRate = AxisRates.MaxOrDefault(AxisRates.First());
                    }
                }
                wasConnected = true;
            } else {
                if(wasConnected) {
                    AxisRates = new List<double>();
                    RaisePropertyChanged(nameof(AxisRates));
                }
                wasConnected = false;
            }
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }
    }
}
