﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using norsu.ass.Server.ViewModels;

namespace norsu.ass.Server.Views
{
    /// <summary>
    /// Interaction logic for Suggestions.xaml
    /// </summary>
    public partial class Suggestions : UserControl
    {
        public Suggestions()
        {
            InitializeComponent();
            
            Messenger.Default.AddListener(Messages.DatabaseRefreshed, () =>
            {
                awooo.Context.Post(d =>
                {
                    DataContext = SuggestionsViewModel.GetInstance();
                },null);
            });
        }
    }
}
