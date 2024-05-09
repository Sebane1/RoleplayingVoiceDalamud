using System.Collections.Generic;

namespace RoleplayingVoiceDalamud.Catalogue {
    public class AnimationPage {
        public string Name { get; set; }
        private List<string> _animationItems = new List<string>();
        private int _selectedIndex;

        public AnimationPage() {
        }

        public int PageNumber { get; set; }
        public int SelectedIndex { get => _selectedIndex; set => _selectedIndex = value; }
        public List<string> AnimationItems { get => _animationItems; set => _animationItems = value; }

        public void Add(string item) {
            _animationItems.Add(item);
        }
    }
}
