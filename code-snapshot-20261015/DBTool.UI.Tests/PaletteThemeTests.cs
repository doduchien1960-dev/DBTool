using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Markup;
using System.Xml.Linq;
using System.IO;
using Xunit;

namespace DBTool.UI.Tests;

public sealed class PaletteThemeTests
{
    [Fact]
    public void Real_templates_keep_popup_items_lists_and_caret_dark_with_white_text()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                // Load the production resources directly: AutoCAD's assembly-level
                // extension attributes cannot be resolved in a standalone WPF test host.
                var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "MainPaletteControl.xaml"));
                XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var resources = new XElement(ns + "ResourceDictionary",
                    new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
                    source.Root!.Element(ns + "UserControl.Resources")!.Elements());
                var dictionary = (ResourceDictionary)XamlReader.Parse(resources.ToString());
                var combo = new ComboBox { ItemsSource = new[] { "Sheet A", "Sheet B" }, SelectedIndex = 0 };
                var list = new ListBox { ItemsSource = new[] { "G1", "G2" }, SelectedIndex = 0 };
                var text = new TextBox { Text = "Input" };
                var panel = new StackPanel(); panel.Children.Add(combo); panel.Children.Add(list); panel.Children.Add(text);
                window = new Window { Content = panel, Resources = dictionary, Width = 350, Height = 200,
                    ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
                window.Show(); window.UpdateLayout();
                Assert.Equal(Colors.White, ((SolidColorBrush)combo.Foreground).Color);
                Assert.Equal(Colors.White, ((SolidColorBrush)text.CaretBrush).Color);
                Assert.Equal(Color.FromRgb(30, 30, 30), ((SolidColorBrush)text.Background).Color);
                combo.IsDropDownOpen = true; window.UpdateLayout();
                var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                var border = Assert.IsType<Border>(popup.Child);
                Assert.Equal(Color.FromRgb(45, 45, 48), ((SolidColorBrush)border.Background).Color);
                Assert.True(border.ActualWidth >= combo.ActualWidth);
                var item = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.Equal(Colors.White, ((SolidColorBrush)item.Foreground).Color);
                combo.IsDropDownOpen = false;
                var listItem = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                listItem.ApplyTemplate();
                var selectedBorder = (Border)listItem.Template.FindName("ListItemBorder", listItem);
                Assert.Equal(Color.FromRgb(14, 99, 156), ((SolidColorBrush)selectedBorder.Background).Color);
            }
            catch (Exception ex) { failure = ex; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
