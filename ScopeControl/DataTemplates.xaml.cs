using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ScopeControl {
    [Export(typeof(ResourceDictionary))]
    partial class DataTemplates : ResourceDictionary {
        public DataTemplates() {
            InitializeComponent();
        }
    }
}
