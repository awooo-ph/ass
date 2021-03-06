﻿using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Devcorner.NIdenticon;
using Devcorner.NIdenticon.BrushGenerators;
using MaterialDesignThemes.Wpf;
using norsu.ass.Models;
using norsu.ass.Server.ViewModels;
using Color = System.Drawing.Color;

namespace norsu.ass.Server.Views
{
    /// <summary>
    /// Interaction logic for NewUserEditor.xaml
    /// </summary>
    public partial class UserEditorDialog : UserControl
    {
        public UserEditorDialog(string title, User user, Visibility levelSelectorVisibility = Visibility.Collapsed)
        {
            InitializeComponent();
            Title.Text = title;
            DataContext = user;
            AccessListBox.Visibility = levelSelectorVisibility;
            user.PropertyChanged += (sender, args) =>
            {
                Button.IsEnabled = user.CanSave();
            };
        }

        internal UserEditorDialog(string title, UserSelector user)
        {
            InitializeComponent();
            Title.Text = title;
            DataContext = user;
            AccessListBox.Visibility = Visibility.Visible;
            AccessListBox.IsHitTestVisible = false;
            user.PropertyChanged += (sender, args) =>
            {
                Button.IsEnabled = !string.IsNullOrEmpty(user.Username) && MainViewModel.Instance.CurrentUser?.Access>user.Access;
            };
        }

        private void UIElement_OnPreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                var file = files?.FirstOrDefault();
                if (!ImageProcessor.IsAccepted(file))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
                e.Effects = DragDropEffects.All;
            }
            e.Effects = DragDropEffects.None;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var rnd = new Random();
            var gen = new IdenticonGenerator()
                .WithBlocks(7, 7)
                .WithSize(174, 174)
                .WithBlockGenerators(IdenticonGenerator.ExtendedBlockGeneratorsConfig)
                .WithBackgroundColor(Color.White)
                .WithBrushGenerator(new StaticColorBrushGenerator(Color.FromArgb(255,rnd.Next(0,255),rnd.Next(0,255),rnd.Next(0,255))));

            using (var pic = gen.Create("awooo" + DateTime.Now.Ticks))
            {
                using (var stream = new MemoryStream())
                {
                    pic.Save(stream, ImageFormat.Jpeg);
                    
                    if(DataContext is User user)
                        user.Picture = stream.ToArray();
                    else if(DataContext is UserSelector selector)
                        selector.Picture = stream.ToArray();
                    
                }
            }

        }

        private void UIElement_OnPreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
                var file = files?.FirstOrDefault();
                if (file == null)
                    return;
                if (!ImageProcessor.IsAccepted(file))
                    return;

                using (var img = System.Drawing.Image.FromFile(file))
                {
                    using (var bmp = ImageProcessor.Resize(img, 128))
                    {
                        using (var bin = new MemoryStream())
                        {
                            bmp.Save(bin, ImageFormat.Jpeg);
                            ((User) DataContext).Picture = bin.ToArray();
                        }
                    }
                }
            }
        }
        
        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is User)
            {
                var usr = (User) DataContext;
                if (string.IsNullOrEmpty(usr.Username)) return;
                if (!usr.CanSave()) return;
                if (usr.Id == 0 && User.Cache.Any(x => x.Username.ToLower() == usr.Username.ToLower()))
                {
                    //TODO dialog
                    MessageBox.Show("Username is already taken.");
                    return;
                }
            } else if (DataContext is UserSelector)
            {
                var selector = (UserSelector)DataContext;
                if (string.IsNullOrEmpty(selector.Username)) return;
                
            }
            else return;
            DialogHost.CloseDialogCommand.Execute(true,this);
        }
    }
}
