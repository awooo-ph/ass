﻿using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Devcorner.NIdenticon;
using Devcorner.NIdenticon.BlockGenerators;
using Devcorner.NIdenticon.BrushGenerators;
using MaterialDesignThemes.Wpf;
using norsu.ass.Models;
using norsu.ass.Network;
using norsu.ass.Server.Views;
using Office = norsu.ass.Models.Office;
using Suggestion = norsu.ass.Models.Suggestion;

namespace norsu.ass.Server.ViewModels
{
    class MainViewModel : ViewModelBase
    {
        private MainViewModel() { }

        private static MainViewModel _instance;
        public static MainViewModel Instance => _instance ?? (_instance = new MainViewModel());

        private ICommand _showAccountCommand;

        public ICommand ShowAccountCommand => _showAccountCommand ?? (_showAccountCommand = new DelegateCommand(d =>
        {
            SidebarIndex = 0;
        }));

        private ICommand _showOfficesCommand;
        public ICommand ShowOfficesCommand => _showOfficesCommand ?? (_showOfficesCommand = new DelegateCommand(d =>
        {
            SidebarIndex = 2;
        }));

        private ICommand _showSettingsCommand;

        public ICommand ShowSettingsCommand => _showSettingsCommand ?? (_showSettingsCommand = new DelegateCommand(d =>
        {
            SidebarIndex = 1;
        }));

        private ICommand _changePasswordCommand;

        public ICommand ChangePasswordCommand =>
            _changePasswordCommand ??
            (_changePasswordCommand = new DelegateCommand(d => DialogHost.Show(new ChangePasswordDialog(CurrentUser))));

        private ICommand _addOfficeCommand;

        public ICommand AddOfficeCommand => _addOfficeCommand ?? (_addOfficeCommand = new DelegateCommand(async d =>
        {
            var offce = new NewOfficeViewModel();
            await DialogHost.Show(offce, "DialogHost", null,
                (sender, args) =>
                {
                    if (args.IsCancelled)
                        return;

                    var ofc = new Office()
                    {
                        ShortName = offce.ShortName,
                        LongName = offce.LongName,
                    };
                    ofc.Save();
                });

            //  if ((bool)result) return;

        }));

        

        private ICommand _logoutCommand;

        public ICommand LogoutCommand => _logoutCommand ?? (_logoutCommand = new DelegateCommand(d =>
        {
            CurrentUser = null;
            SelectedIndex = 0;
        }));

        private ICommand _loginCommand;

        public ICommand LoginCommand => _loginCommand ?? (_loginCommand = new DelegateCommand<PasswordBox>(d =>
        {
            var user = Models.User.Cache.FirstOrDefault(x => x.Username?.ToLower() == Username.ToLower());
            if (user == null && Models.User.Cache.Count(x => x.Access == User.AccessLevels.SuperAdmin) == 0)
            {
                var gen = new IdenticonGenerator()
                    .WithBlocks(7, 7)
                    .WithSize(128, 128)
                    .WithBlockGenerators(IdenticonGenerator.ExtendedBlockGeneratorsConfig)
                    .WithBackgroundColor(Color.White)
                    .WithBrushGenerator(new StaticColorBrushGenerator(Color.Red));
                
                using (var pic = gen.Create(Username+DateTime.Now.Ticks))
                {
                    using (var stream = new MemoryStream())
                    {
                            pic.Save(stream, ImageFormat.Jpeg);
                        user = new User()
                        {
                            Username = Username,
                            Password = d.Password,
                            Access = User.AccessLevels.SuperAdmin,
                            Picture = stream.ToArray(),
                        };
                        user.Save();
                    }
                }
            }

            if (user == null || user?.Password != d.Password)
            {
                MessageBox.Show("Invalid username or password.", "Login Failed", 
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
                d.SelectAll();
                d.Focus();
                return;
            }

            CurrentUser = user;
            
            d.Password = string.Empty;
            Username = "";
            SelectedIndex = 1;
        }));

        private Models.User _CurrentUser;

        public Models.User CurrentUser
        {
            get => _CurrentUser;
            set
            {
                if(value == _CurrentUser)
                    return;
                _CurrentUser = value;
                OnPropertyChanged(nameof(CurrentUser));
            }
        }

        private int _SidebarIndex = 2;

        public int SidebarIndex
        {
            get => _SidebarIndex;
            set
            {
                if(value == _SidebarIndex)
                    return;
                _SidebarIndex = value;
                OnPropertyChanged(nameof(SidebarIndex));
            }
        }
        
        private int _SelectedIndex = 0;

        public int SelectedIndex
        {
            get => _SelectedIndex;
            set
            {
                if(value == _SelectedIndex)
                    return;
                _SelectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
            }
        }

        private string _Password;

        public string Password
        {
            get => _Password;
            set
            {
                if(value == _Password)
                    return;
                _Password = value;
                OnPropertyChanged(nameof(Password));
            }
        }

        private string _Username;

        public string Username
        {
            get => _Username;
            set
            {
                if(value == _Username)
                    return;
                _Username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        private ListCollectionView _offices;

        public ListCollectionView Offices
        {
            get
            {
                if (_offices != null) return _offices;                
                _offices = new ListCollectionView(Office.Cache);
                _offices.CurrentChanged += (sender, args) =>
                {
                    Suggestions.Filter = FilterSuggestion;
                    RatingsChanged();
                };
                Rating.Cache.CollectionChanged += (sender, args) =>
                {
                    RatingsChanged();
                };
                Suggestion.Cache.CollectionChanged += (sender, args) =>
                {
                    Suggestions.Filter = FilterSuggestion;
                };
                return _offices;
            }
        }

        private void RatingsChanged()
        {
            Ratings.Filter = FilterRating;
            OnPropertyChanged(nameof(OneStar));
            OnPropertyChanged(nameof(TwoStars));
            OnPropertyChanged(nameof(ThreeStars));
            OnPropertyChanged(nameof(FourStars));
            OnPropertyChanged(nameof(FiveStars));
            OnPropertyChanged(nameof(LatestRating));
            OnPropertyChanged(nameof(LatestSuggestion));
            OnPropertyChanged(nameof(TopSuggestion));
            OfficeAdmins.Filter = FilterOfficeAdmins;
        }

        private bool FilterOfficeAdmins(object o)
        {
            if (Offices.CurrentItem == null) return false;
            var adm = o as User;
            return adm?.Access == User.AccessLevels.OfficeAdmin && adm?.OfficeId == ((Office) Offices.CurrentItem).Id;
        }

        private ListCollectionView _officeAdmins;

        public ListCollectionView OfficeAdmins
        {
            get
            {
                if (_officeAdmins != null) return _officeAdmins;
                _officeAdmins = new ListCollectionView(User.Cache);
                _officeAdmins.Filter = FilterOfficeAdmins;
                return _officeAdmins;
            }
        }
        
        public Rating LatestRating => Rating.Cache
            .OrderByDescending(x => x.Time)
            .FirstOrDefault(x => x.OfficeId ==((Office)Offices.CurrentItem).Id);

        public Suggestion LatestSuggestion => Suggestion.Cache
            .OrderByDescending(x => x.Time)
            .FirstOrDefault(x => x.OfficeId == ((Office) Offices.CurrentItem).Id);
        
        public Suggestion TopSuggestion => Suggestion.Cache
            .OrderByDescending(x => x.Votes)
            .FirstOrDefault(x => x.OfficeId == ((Office) Offices.CurrentItem).Id);
        
        //  public long RatingCount => Rating.Cache.Count(d => d.OfficeId == ((Office) Offices.CurrentItem).Id);
        public long OneStar => Rating.Cache.Count(d=>d.Value==1 && d.OfficeId==((Office) Offices.CurrentItem).Id);
        public long TwoStars => Rating.Cache.Count(d => d.Value == 2 && d.OfficeId == ((Office) Offices.CurrentItem).Id);

        public long ThreeStars =>
            Rating.Cache.Count(d => d.Value == 3 && d.OfficeId == ((Office) Offices.CurrentItem).Id);

        public long FourStars =>
            Rating.Cache.Count(d => d.Value == 4 && d.OfficeId == ((Office) Offices.CurrentItem).Id);

        public long FiveStars =>
            Rating.Cache.Count(d => d.Value == 5 && d.OfficeId == ((Office) Offices.CurrentItem).Id);
        
        private ListCollectionView _suggestions;

        public ListCollectionView Suggestions
        {
            get
            {
                if (_suggestions != null) return _suggestions;
                _suggestions = new ListCollectionView(Suggestion.Cache);
                _suggestions.Filter = FilterSuggestion;
                Office.Cache.CollectionChanged += (sender, args) =>
                {
                    _suggestions.Filter = FilterSuggestion;
                };
                return _suggestions;
            }
        }

        private bool FilterSuggestion(object o)
        {
            if (!(o is Suggestion msg)) return false;
            var selectedOffice = Offices.CurrentItem as Office;
            return msg.OfficeId == selectedOffice?.Id;
        }

        private ListCollectionView _ratings;

        public ListCollectionView Ratings
        {
            get
            {
                if (_ratings != null) return _ratings;
                _ratings = new ListCollectionView(Rating.Cache);
                _ratings.Filter = FilterRating;
                Office.Cache.CollectionChanged += (sender, args) =>
                {
                    _ratings.Filter = FilterRating;
                };
                return _ratings;
            }
        }

        private bool FilterRating(object o)
        {
            if (!(o is Rating rating)) return false;
            return rating.OfficeId == (Offices.CurrentItem as Office)?.Id;
        }

        private string _ReplyText;

        public string ReplyText
        {
            get => _ReplyText;
            set
            {
                if(value == _ReplyText)
                    return;
                _ReplyText = value;
                OnPropertyChanged(nameof(ReplyText));
            }
        }

        private ICommand _replyCommand;

        public ICommand ReplyCommand => _replyCommand ?? (_replyCommand = new DelegateCommand(msg =>
        {
            new Models.Comment
            {
                SuggestionId = ((Suggestion) Suggestions.CurrentItem).Id,
                ParentId = 0,
                Message = ReplyText,
                UserId = CurrentUser.Id
            }.Save();
            ReplyText = string.Empty;
        }));
    }
}
