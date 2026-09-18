using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.Admin.UserActivityInsights.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.UserActivityInsights.Views
{
    public partial class UserActivityInsightsView : UserControl
    {
        public UserActivityInsightsView(UserActivityFilter initialFilter = null)
        {
            InitializeComponent();
            DataContext = new UserActivityInsightsViewModel(initialFilter);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var vm = (UserActivityInsightsViewModel)DataContext;

            ChartMostActiveHours.Content    = MakeCartesian(vm.MostActiveHoursSeries,    xAxes: vm.MostActiveHoursXAxes);
            ChartActivitiesOverTime.Content  = MakeCartesian(vm.ActivitiesOverTimeSeries,  xAxes: vm.ActivitiesOverTimeXAxes);
            ChartEntityDistribution.Content  = MakePie(vm.EntityDistributionSeries);
            ChartMonthlyBreakdown.Content    = MakeCartesian(vm.MonthlyBreakdownSeries,    xAxes: vm.MonthlyBreakdownXAxes, legendRight: true);
            ChartActivitiesByAction.Content  = MakePie(vm.ActivitiesByActionSeries);
            ChartTopUsers.Content            = MakeCartesian(vm.TopUsersSeries,            yAxes: vm.TopUsersYAxes);
        }

        private static object MakeCartesian(object series, object xAxes = null, object yAxes = null, bool legendRight = false)
        {
            try
            {
                var asm  = Assembly.Load("LiveChartsCore.SkiaSharpView.WPF");
                var type = asm.GetType("LiveChartsCore.SkiaSharpView.WPF.CartesianChart");
                var chart = Activator.CreateInstance(type);
                type.GetProperty("Series")?.SetValue(chart, series);
                if (xAxes != null) type.GetProperty("XAxes")?.SetValue(chart, xAxes);
                if (yAxes != null) type.GetProperty("YAxes")?.SetValue(chart, yAxes);
                if (legendRight)
                {
                    var legendProp = type.GetProperty("LegendPosition");
                    if (legendProp != null)
                    {
                        var pos = Enum.Parse(legendProp.PropertyType, "Right");
                        legendProp.SetValue(chart, pos);
                    }
                }
                return chart;
            }
            catch (Exception ex)
            {
                return new TextBlock { Text = $"Chart unavailable: {ex.Message}", Foreground = System.Windows.Media.Brushes.Gray };
            }
        }

        private static object MakePie(object series)
        {
            try
            {
                var asm  = Assembly.Load("LiveChartsCore.SkiaSharpView.WPF");
                var type = asm.GetType("LiveChartsCore.SkiaSharpView.WPF.PieChart");
                var chart = Activator.CreateInstance(type);
                type.GetProperty("Series")?.SetValue(chart, series);
                return chart;
            }
            catch (Exception ex)
            {
                return new TextBlock { Text = $"Chart unavailable: {ex.Message}", Foreground = System.Windows.Media.Brushes.Gray };
            }
        }
    }
}
