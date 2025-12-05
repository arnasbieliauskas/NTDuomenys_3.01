using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CompiledAvaloniaXaml;
using RpaAruodas.Configuration;
using RpaAruodas.Services;

namespace RpaAruodas;

public partial class MainWindow : Window
{
	public sealed class StatsEntry
	{
		private static readonly IBrush PositiveHistoryBrush = new SolidColorBrush(Color.Parse("#ffc5c5"));

		private static readonly IBrush NegativeHistoryBrush = new SolidColorBrush(Color.Parse("#b1f2c4"));

		private static readonly IBrush DefaultHistoryBrush = new SolidColorBrush(Color.Parse("#1d4ed8"));

		private static readonly IBrush DefaultHistoryForeground = Brushes.White;

		private static readonly IBrush FavoriteBrushActive = new SolidColorBrush(Color.Parse("#f59e0b"));

		private static readonly IBrush FavoriteBrushInactive = new SolidColorBrush(Color.Parse("#cbd5e1"));

		private readonly StatsListing _source;

		public string CollectedOn { get; }

		public string City { get; }

		public string Address { get; }

		public string Price { get; }

		public string PricePerSquare { get; }

		public string Rooms { get; }

		public string SearchObject { get; }

		public bool Selected { get; private set; }

		public string InlineLine { get; }

		public string OfferLabel { get; }

		public string AdvertisementUrl { get; }

		public bool HasUrl => !string.IsNullOrWhiteSpace(AdvertisementUrl);

		public string ExternalId { get; }

		public int VersionCount { get; }

		public bool HasHistory => !string.IsNullOrWhiteSpace(ExternalId) && VersionCount > 1;

		public double? PriceChangePercent { get; }

		public string PriceChange => FormatPercent(PriceChangePercent);

		public string PricePerSquareChange => string.Empty;

		public IBrush PriceChangeColor => GetChangeBrush(PriceChangePercent);

		public IBrush PricePerSquareChangeColor => Brushes.Transparent;

		public IBrush? HistoryBackground
		{
			get
			{
				if (!HasHistory)
				{
					return null;
				}
				if (IsNoPriceChange(PriceChangePercent))
				{
					return DefaultHistoryBrush;
				}
				return (PriceChangePercent > 0.0) ? PositiveHistoryBrush : NegativeHistoryBrush;
			}
		}

		public IBrush? HistoryForeground
		{
			get
			{
				if (!HasHistory)
				{
					return null;
				}
				if (IsNoPriceChange(PriceChangePercent))
				{
					return DefaultHistoryForeground;
				}
				return (PriceChangePercent > 0.0) ? Brushes.White : Brushes.Black;
			}
		}

		public string FavoriteGlyph => Selected ? "★" : "☆";

		public IBrush FavoriteBrush => Selected ? FavoriteBrushActive : FavoriteBrushInactive;

		public StatsEntry(StatsListing listing)
		{
			_source = listing;
			CollectedOn = NormalizeDate(listing.CollectedOn);
			City = FormatValue(listing.SearchCity);
			Address = FormatValue(listing.Address);
			AdvertisementUrl = listing.AdvertisementUrl;
			Price = FormatValue(listing.Price);
			PricePerSquare = FormatValue(listing.PricePerSquare);
			Rooms = FormatValue(listing.Rooms);
			SearchObject = listing.SearchObject;
			ExternalId = listing.ExternalId;
			Selected = listing.Selected;
			VersionCount = listing.VersionCount;
			PriceChangePercent = listing.PriceChangePercent;
			InlineLine = BuildInlineLine();
			OfferLabel = BuildOfferLabel(listing);
		}

		public void SetSelected(bool selected)
		{
			Selected = selected;
		}

		public StatsListing ToListingWithSelected(bool selected)
		{
			return new StatsListing
			{
				CollectedOn = _source.CollectedOn,
				SearchObject = _source.SearchObject,
				SearchCity = _source.SearchCity,
				MicroDistrict = _source.MicroDistrict,
				Address = _source.Address,
				Price = _source.Price,
				PricePerSquare = _source.PricePerSquare,
				Rooms = _source.Rooms,
				AreaSquare = _source.AreaSquare,
				AreaLot = _source.AreaLot,
				HouseState = _source.HouseState,
				OfferType = _source.OfferType,
				Intendances = _source.Intendances,
				Floors = _source.Floors,
				AdvertisementUrl = _source.AdvertisementUrl,
				ExternalId = _source.ExternalId,
				Selected = selected,
				VersionCount = _source.VersionCount,
				PriceChangePercent = _source.PriceChangePercent
			};
		}

		private static string FormatPercent(double? value)
		{
			if (!value.HasValue)
			{
				return string.Empty;
			}
			double value2 = value.Value;
			bool flag = false;
			string result = ((value2 > 0.0) ? $"+{value.Value:0.##}%" : ((!(value2 < 0.0)) ? "0%" : $"{value.Value:0.##}%"));
			bool flag2 = false;
			return result;
		}

		private static IBrush GetChangeBrush(double? value)
		{
			if (!value.HasValue || IsNoPriceChange(value))
			{
				return Brushes.Gray;
			}
			return (value.Value > 0.0) ? Brushes.OrangeRed : Brushes.SeaGreen;
		}

		private static bool IsNoPriceChange(double? value)
		{
			return !value.HasValue || Math.Abs(value.Value) < 0.0001;
		}

		private static string NormalizeDate(string collectedOn)
		{
			DateTime result;
			return DateTime.TryParse(collectedOn, out result) ? result.ToString("yyyy-MM-dd") : collectedOn;
		}

		private static string FormatValue(string? value)
		{
			return string.IsNullOrWhiteSpace(value) ? "-" : value;
		}

		private string BuildInlineLine()
		{
			string[] value = new string[6]
			{
				FormatValue(CollectedOn),
				City,
				Address,
				Price,
				PricePerSquare,
				Rooms
			};
			return string.Join(" | ", value);
		}

		private static string BuildOfferLabel(StatsListing listing)
		{
			string[] array = new string[8]
			{
				FormatValue(listing.SearchObject),
				FormatValue(listing.OfferType),
				FormatValue(listing.MicroDistrict),
				FormatValue(listing.AreaSquare),
				FormatValue(listing.AreaLot),
				FormatValue(listing.HouseState),
				FormatValue(listing.Intendances),
				FormatValue(listing.Floors)
			};
			return array.Any((string p) => p != "-") ? string.Join(" | ", array) : "-";
		}
	}

	private sealed class CompareStatsWindow : Window
	{
		private readonly IDatabaseService _databaseService;

		private readonly ILogService _logService;

		private readonly ObservableCollection<string> _objectOptions = new ObservableCollection<string>();

		private readonly ObservableCollection<string> _roomOptions = new ObservableCollection<string>();

		private readonly ObservableCollection<string> _cityOptions = new ObservableCollection<string>();

		private static bool _compareStylesRegistered;

	private ComboBox _objectCombo = null;

	private ComboBox _roomsCombo = null;

	private readonly List<ComboBox> _cityCombos = new List<ComboBox>();

	private StackPanel _cityComboPanel = null;

	private CheckBox _compareAvgPriceCheckBox = null;

	private CheckBox _compareAvgPricePerSquareCheckBox = null;

		private DatePicker _fromDate = null;

		private DatePicker _toDate = null;

		private Button _removeCityButton = null;

		private bool _suppressChanges;

		public CompareStatsWindow(IDatabaseService databaseService, ILogService logService)
		{
			_databaseService = databaseService;
			_logService = logService;
			base.Width = 1120.0;
			base.Height = 420.0;
			base.Background = new SolidColorBrush(Color.Parse("#f6f7fb"));
			base.Title = "Palyginti statistika";
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			base.CanResize = false;
			EnsureCompareStyles();
			base.Content = BuildContent();
			base.Opened += async delegate
			{
				await LoadFiltersAsync();
			};
		}

		private void EnsureCompareStyles()
		{
			if (!_compareStylesRegistered)
			{
				_compareStylesRegistered = true;
				Style item = new Style((Selector? x) => x.OfType<Button>().Class("primary"))
				{
					Setters = 
					{
						(SetterBase)new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.Parse("#1d4ed8"))),
						(SetterBase)new Setter(TemplatedControl.ForegroundProperty, Brushes.White),
						(SetterBase)new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(Color.Parse("#1d4ed8"))),
						(SetterBase)new Setter(TemplatedControl.FontSizeProperty, 20.0),
						(SetterBase)new Setter(TemplatedControl.FontWeightProperty, FontWeight.DemiBold),
						(SetterBase)new Setter(TemplatedControl.PaddingProperty, new Thickness(32.0, 18.0)),
						(SetterBase)new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0.0)),
						(SetterBase)new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left),
						(SetterBase)new Setter(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center),
						(SetterBase)new Setter(Visual.EffectProperty, null),
						(SetterBase)new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<Button>(delegate(Button control, INameScope _)
						{
							Border border = new Border
							{
								Name = "primaryChrome",
								Width = control.Width,
								Height = control.Height,
								MinWidth = control.MinWidth,
								MinHeight = control.MinHeight,
								CornerRadius = new CornerRadius(999.0),
								HorizontalAlignment = control.HorizontalAlignment,
								VerticalAlignment = control.VerticalAlignment
							};
							border.Bind(Border.BackgroundProperty, control.GetObservable(TemplatedControl.BackgroundProperty));
							border.Bind(Border.BorderBrushProperty, control.GetObservable(TemplatedControl.BorderBrushProperty));
							border.Bind(Border.BorderThicknessProperty, control.GetObservable(TemplatedControl.BorderThicknessProperty));
							border.Bind(Decorator.PaddingProperty, control.GetObservable(TemplatedControl.PaddingProperty));
							ContentPresenter contentPresenter = new ContentPresenter
							{
								HorizontalAlignment = HorizontalAlignment.Center,
								VerticalAlignment = VerticalAlignment.Center
							};
							contentPresenter.Bind(ContentPresenter.ContentProperty, control.GetObservable(ContentControl.ContentProperty));
							contentPresenter.Bind(ContentPresenter.ForegroundProperty, control.GetObservable(TemplatedControl.ForegroundProperty));
							border.Child = contentPresenter;
							return border;
						}))
					}
				};
				Style item2 = new Style((Selector? x) => x.OfType<Button>().Class("primary").PropertyEquals(InputElement.IsPointerOverProperty, true))
				{
					Setters = 
					{
						(SetterBase)new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.Parse("#e2e8f0"))),
						(SetterBase)new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(Color.Parse("#cbd5f5"))),
						(SetterBase)new Setter(TemplatedControl.ForegroundProperty, new SolidColorBrush(Color.Parse("#1e3a8a"))),
						(SetterBase)new Setter(Visual.EffectProperty, new DropShadowEffect
						{
							BlurRadius = 18.0,
							OffsetX = 0.0,
							OffsetY = 5.0,
							Opacity = 0.3,
							Color = Color.Parse("#94a3b8")
						})
					}
				};
				base.Styles.Add(item);
				base.Styles.Add(item2);
			}
		}

		private Control BuildContent()
		{
			_objectCombo = BuildCombo();
			_roomsCombo = BuildCombo();
			_cityComboPanel = new StackPanel
			{
				Spacing = 8.0,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			AddCityCombo();
			_fromDate = new DatePicker
			{
				Width = 160.0
			};
			_toDate = new DatePicker
			{
				Width = 160.0
			};
			_objectCombo.SelectionChanged += async delegate
			{
				await RefreshOptionsAsync();
			};
			_roomsCombo.SelectionChanged += async delegate
			{
				await RefreshOptionsAsync();
			};
			Grid grid = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("Auto,*"),
				ColumnSpacing = 24.0,
				VerticalAlignment = VerticalAlignment.Top,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			StackPanel stackPanel = new StackPanel
			{
				Spacing = 8.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
			};
			Button button = new Button
			{
				Content = "Pridėti miestą/gyvenvietę",
				MinWidth = 200.0,
				Padding = new Thickness(18.0, 12.0),
				FontSize = 14.0,
				FontWeight = FontWeight.DemiBold,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				Cursor = new Cursor(StandardCursorType.Hand)
			};
			button.Classes.Add("primary");
			button.Click += delegate
			{
				AddCityCombo();
			};
			_removeCityButton = new Button
			{
				Content = "Pašalinti miestą/gyvenvietę",
				MinWidth = 200.0,
				Padding = new Thickness(18.0, 12.0),
				FontSize = 14.0,
				FontWeight = FontWeight.DemiBold,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				Cursor = new Cursor(StandardCursorType.Hand)
			};
			_removeCityButton.Classes.Add("primary");
			_removeCityButton.Click += delegate
			{
				RemoveCityCombo();
			};
			stackPanel.Children.Add(button);
			stackPanel.Children.Add(_removeCityButton);
			_compareAvgPriceCheckBox = CreateCompareOption("Vidutinę pardavimo kainą");
			_compareAvgPricePerSquareCheckBox = CreateCompareOption("Vidutinę €/m²");
			stackPanel.Children.Add(new Border
			{
				Padding = new Thickness(12.0),
				Background = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.Parse("#e2e8f0")),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(16.0),
				Margin = new Thickness(0.0, 8.0, 0.0, 0.0),
				Child = new StackPanel
				{
					Spacing = 6.0,
					Children = 
					{
						(Control)new TextBlock
						{
							Text = "Palyginti pagal:",
							FontSize = 14.0,
							FontWeight = FontWeight.DemiBold,
							Foreground = new SolidColorBrush(Color.Parse("#0f172a"))
						},
						(Control)_compareAvgPriceCheckBox,
						(Control)_compareAvgPricePerSquareCheckBox
					}
				}
			});
			Grid.SetColumn(stackPanel, 0);
			grid.Children.Add(stackPanel);
			Grid grid2 = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,*"),
				RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
				ColumnSpacing = 24.0,
				RowSpacing = 16.0
			};
			StackPanel stackPanel2 = BuildField("Objekto tipas", _objectCombo);
			Grid.SetColumn(stackPanel2, 0);
			Grid.SetRow(stackPanel2, 0);
			grid2.Children.Add(stackPanel2);
			StackPanel stackPanel3 = BuildField("Kambariai", _roomsCombo);
			Grid.SetColumn(stackPanel3, 1);
			Grid.SetRow(stackPanel3, 0);
			grid2.Children.Add(stackPanel3);
			StackPanel stackPanel4 = BuildCityField();
			Grid.SetColumn(stackPanel4, 0);
			Grid.SetRow(stackPanel4, 1);
			grid2.Children.Add(stackPanel4);
			StackPanel stackPanel5 = new StackPanel
			{
				Spacing = 6.0
			};
			stackPanel5.Children.Add(new TextBlock
			{
				Text = "Dat nuo / iki",
				FontSize = 14.0,
				Foreground = new SolidColorBrush(Color.Parse("#475467"))
			});
			StackPanel stackPanel6 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 8.0
			};
			stackPanel6.Children.Add(_fromDate);
			stackPanel6.Children.Add(_toDate);
			stackPanel5.Children.Add(stackPanel6);
			Grid.SetColumn(stackPanel5, 1);
			Grid.SetRow(stackPanel5, 1);
			grid2.Children.Add(stackPanel5);
			Grid.SetColumn(grid2, 1);
			grid.Children.Add(grid2);
			Border content = new Border
			{
				Background = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.Parse("#e2e8f0")),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(16.0),
				Padding = new Thickness(24.0),
				Child = grid
			};

			ScrollViewer scrollViewer = new ScrollViewer
			{
				Content = content,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};

			StackPanel footerPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(16.0, 12.0, 16.0, 16.0),
				Spacing = 8.0
			};

			Button compareGraphButton = new Button
			{
				Content = "Grafikas",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(26.0, 14.0),
				MinWidth = 160.0,
				FontSize = 20.0
			};
			compareGraphButton.Classes.Add("primary");
			compareGraphButton.Click += OnCompareGraphClick;

			footerPanel.Children.Add(compareGraphButton);

			Grid root = new Grid
			{
				RowDefinitions = new RowDefinitions("*,Auto")
			};
			Grid.SetRow(scrollViewer, 0);
			Grid.SetRow(footerPanel, 1);
			root.Children.Add(scrollViewer);
			root.Children.Add(footerPanel);

			return root;
		}

		private ComboBox BuildCombo()
		{
			return new ComboBox
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Width = double.NaN
			};
		}

		private static StackPanel BuildField(string label, Control input)
		{
			return new StackPanel
			{
				Spacing = 6.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Children = 
				{
					(Control)new TextBlock
					{
						Text = label,
						FontSize = 16.0,
						Foreground = new SolidColorBrush(Color.Parse("#475467"))
					},
					input
				}
			};
		}

		private StackPanel BuildCityField()
		{
			return new StackPanel
			{
				Spacing = 6.0,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Children = 
				{
					(Control)new TextBlock
					{
						Text = "Miestas / gyvenviete",
						FontSize = 16.0,
						Foreground = new SolidColorBrush(Color.Parse("#475467"))
					},
					(Control)_cityComboPanel
				}
			};
		}

		private CheckBox CreateCompareOption(string label)
		{
			CheckBox checkBox = new CheckBox
			{
				Content = label,
				IsThreeState = false
			};
			checkBox.Classes.Add("compare-option");
			return checkBox;
		}

		private ComboBox AddCityCombo()
		{
			ComboBox comboBox = BuildCombo();
			comboBox.ItemsSource = _cityOptions;
			comboBox.SelectionChanged += async delegate
			{
				await RefreshOptionsAsync();
			};
			_cityCombos.Add(comboBox);
			_cityComboPanel.Children.Add(comboBox);
			SetSelection(comboBox, _cityOptions, string.Empty);
			UpdateRemoveCityButtonState();
			return comboBox;
		}

		private void RemoveCityCombo()
		{
			if (_cityCombos.Count > 1)
			{
				ComboBox item = _cityCombos.Last();
				_cityCombos.RemoveAt(_cityCombos.Count - 1);
				_cityComboPanel.Children.Remove(item);
				UpdateRemoveCityButtonState();
			}
		}

		private void UpdateRemoveCityButtonState()
		{
			if (_removeCityButton != null)
			{
				_removeCityButton.IsEnabled = _cityCombos.Count > 1;
			}
		}

		private async Task LoadFiltersAsync()
		{
			_objectCombo.ItemsSource = _objectOptions;
			_roomsCombo.ItemsSource = _roomOptions;
			foreach (ComboBox cityCombo in _cityCombos)
			{
				cityCombo.ItemsSource = _cityOptions;
			}
			await RefreshOptionsAsync();
		}

		private async Task RefreshOptionsAsync()
		{
			if (_suppressChanges)
			{
				return;
			}
			_suppressChanges = true;
			try
			{
				string currentObject = NormalizeSelection(_objectCombo.SelectedItem as string);
				string currentCity = GetPrimaryCitySelection();
				string currentRooms = NormalizeSelection(_roomsCombo.SelectedItem as string);
				await UpdateObjectOptionsAsync(currentCity, currentObject);
				await UpdateCityOptionsAsync(currentObject, currentCity);
				await UpdateRoomOptionsAsync(currentObject, currentCity, currentRooms);
			}
			finally
			{
				_suppressChanges = false;
			}
		}

		private async Task UpdateObjectOptionsAsync(string? city, string? currentSelection)
		{
			_objectCombo.SelectedIndex = -1;
			_objectOptions.Clear();
			_objectOptions.Add(string.Empty);
			try
			{
				foreach (string item in await _databaseService.GetDistinctSearchObjectsAsync(city, CancellationToken.None))
				{
					_objectOptions.Add(item);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				_logService.Error("Nepavyko uzkrauti objekto saraso (palyginimas).", ex2);
			}
			SetSelection(_objectCombo, _objectOptions, currentSelection);
		}

		private async Task UpdateCityOptionsAsync(string? obj, string? currentSelection)
		{
			List<string> selections = _cityCombos.Select((ComboBox c) => NormalizeSelection(c.SelectedItem as string)).ToList();
			foreach (ComboBox cityCombo in _cityCombos)
			{
				cityCombo.SelectedIndex = -1;
			}
			_cityOptions.Clear();
			_cityOptions.Add(string.Empty);
			try
			{
				foreach (string item in await _databaseService.GetDistinctSearchCitiesAsync(obj, CancellationToken.None))
				{
					_cityOptions.Add(item);
				}
			}
			catch (Exception exception)
			{
				_logService.Error("Nepavyko uzkrauti miestu saraso (palyginimas).", exception);
			}
			for (int i = 0; i < _cityCombos.Count; i++)
			{
				SetSelection(value: (i < selections.Count) ? selections[i] : currentSelection, combo: _cityCombos[i], items: _cityOptions);
			}
		}

		private async Task UpdateRoomOptionsAsync(string? obj, string? city, string? currentSelection)
		{
			_roomsCombo.SelectedIndex = -1;
			_roomOptions.Clear();
			_roomOptions.Add(string.Empty);
			try
			{
				foreach (string item in await _databaseService.GetDistinctRoomsAsync(obj, city, null, CancellationToken.None))
				{
					_roomOptions.Add(item);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				_logService.Error("Nepavyko uzkrauti kambariu saraso (palyginimas).", ex2);
			}
			SetSelection(_roomsCombo, _roomOptions, currentSelection);
		}

		private static void SetSelection(ComboBox combo, ObservableCollection<string> items, string? value)
		{
			if (items.Count == 0)
			{
				combo.SelectedIndex = -1;
			}
			else if (!string.IsNullOrWhiteSpace(value) && items.Contains(value))
			{
				combo.SelectedItem = value;
			}
			else
			{
				combo.SelectedIndex = 0;
			}
		}

		private static string NormalizeSelection(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}
			return value.Trim();
		}

		private async void OnCompareGraphClick(object? sender, RoutedEventArgs e)
		{
			try
			{
				if (_compareAvgPriceCheckBox == null || _compareAvgPricePerSquareCheckBox == null)
				{
					_logService.Info("Grafiko mygtukas nebuvo inicijuotas.");
					return;
				}

				bool showAvgPrice = _compareAvgPriceCheckBox.IsChecked == true;
				bool showAvgPricePerSq = _compareAvgPricePerSquareCheckBox.IsChecked == true;
				if (!showAvgPrice && !showAvgPricePerSq)
				{
					_logService.Info("Pasirinkite bent vieną palyginimo kriterijų.");
					return;
				}

				List<string> cities = _cityCombos.Select((ComboBox combo) => NormalizeSelection(combo.SelectedItem as string))
					.Where((string value) => !string.IsNullOrWhiteSpace(value))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (cities.Count == 0)
				{
					_logService.Info("Pasirinkite bent vieną miestą.");
					return;
				}

				string? objectType = NormalizeSelection(_objectCombo.SelectedItem as string);
				string? rooms = NormalizeSelection(_roomsCombo.SelectedItem as string);
				DateTime? fromDate = _fromDate.SelectedDate?.DateTime.Date;
				DateTime? toDate = _toDate.SelectedDate?.DateTime.Date;
				IReadOnlyList<CityHistoryEntry> history = await _databaseService.GetCityHistoryAsync(cities, objectType, null, rooms, fromDate, toDate, CancellationToken.None);
				if (history.Count == 0)
				{
					_logService.Info("Pasirinkti filtrai neturi istorinių duomenų.");
					return;
				}

				List<LineSeries> seriesList = new List<LineSeries>();
				foreach (IGrouping<string, CityHistoryEntry> grouping in history.GroupBy((CityHistoryEntry entry) => entry.City, StringComparer.OrdinalIgnoreCase))
				{
					IReadOnlyList<CityHistoryEntry> ordered = grouping.OrderBy((CityHistoryEntry entry) => entry.Date).ToList();
					if (showAvgPrice)
					{
						List<(DateTime date, double value)> pricePoints = ordered.Where((CityHistoryEntry entry) => entry.AveragePrice.HasValue).Select((CityHistoryEntry entry) => (entry.Date, entry.AveragePrice!.Value)).ToList();
						if (pricePoints.Count > 0)
						{
							seriesList.Add(new LineSeries(grouping.Key + " – Vidutinė kaina", pricePoints));
						}
					}

					if (showAvgPricePerSq)
					{
						List<(DateTime date, double value)> perSqPoints = ordered.Where((CityHistoryEntry entry) => entry.AveragePricePerSquare.HasValue).Select((CityHistoryEntry entry) => (entry.Date, entry.AveragePricePerSquare!.Value)).ToList();
						if (perSqPoints.Count > 0)
						{
							seriesList.Add(new LineSeries(grouping.Key + " – €/m²", perSqPoints));
						}
					}
				}

				if (seriesList.Count == 0)
				{
					_logService.Info("Pasirinkti kriterijai neturi pakankamai duomenų grafike.");
					return;
				}

				int totalListings = await GetTotalListingCountAsync(cities, objectType, rooms, null, fromDate, toDate);
				string resultsText = $"Rezultatai: {totalListings}";
				await MainWindow.ShowLineChartAsync(this, seriesList, "Palyginimo grafikas", resultsText);
			}
			catch (Exception ex)
			{
				_logService.Error("Nepavyko sugeneruoti palyginimo grafiko.", ex);
			}
		}

		private async Task<int> GetTotalListingCountAsync(IEnumerable<string> cities, string? objectType, string? rooms, string? microDistrict, DateTime? fromDate, DateTime? toDate)
		{
			if (cities == null)
			{
				return 0;
			}

			int total = 0;
			foreach (string city in cities.Where((string value) => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				StatsQueryResult result = await _databaseService.QueryListingsAsync(
					searchObject: objectType,
					searchCity: city,
					microDistrict: microDistrict,
					address: null,
					fromDate: fromDate,
					toDate: toDate,
					priceFrom: null,
					priceTo: null,
					pricePerSquareFrom: null,
					pricePerSquareTo: null,
					areaFrom: null,
					areaTo: null,
					areaLotFrom: null,
					areaLotTo: null,
					rooms: rooms,
					houseState: null,
					limit: 1,
					offset: 0,
					onlyWithoutHistory: false,
					onlyFavorites: false,
					onlyPriceDrop: false,
					onlyPriceIncrease: false,
					orderByPriceDescending: false,
					orderByPriceAscending: false,
					cancellationToken: CancellationToken.None);
				total += result.TotalCount;
			}

			return total;
		}

		private string GetPrimaryCitySelection()
		{
			if (_cityCombos.Count == 0)
			{
				return string.Empty;
			}
			return NormalizeSelection(_cityCombos[0].SelectedItem as string);
		}
	}

	public const double BrowserViewportWidth = 1200.0;

	public const double BrowserViewportHeight = 1800.0;

	private readonly ILogService _logService;

	private readonly IConfigurationService _configurationService;

	private readonly IPlaywrightRunner _playwrightRunner;

	private readonly IAruodasAutomationService _automationService;

	private readonly IDatabaseService _databaseService;

	private readonly Image _previewImage;

	private readonly Control _previewPlaceholder;

	private readonly Button _aruodasButton;

	private readonly Button _statsButton;

	private readonly Grid _contentRoot;

	private readonly Border _busyOverlay;

	private readonly Control _browserSurface;

	private readonly Control _statsContainer;

	private readonly TextBlock _busyTitle;

	private readonly TextBlock _busyDetails;

	private readonly Button _continueButton;

	private readonly CancellationTokenSource _browserLifetimeCts = new CancellationTokenSource();

	private readonly ComboBox _statsObjectCombo;

	private readonly ComboBox _statsCityCombo;

	private readonly ComboBox _statsMicroDistrictCombo;

	private readonly ComboBox _statsAddressCombo;

	private readonly ComboBox _statsRoomsCombo;

	private readonly ComboBox _statsHouseStateCombo;

	private readonly TextBox _statsPriceFrom;

	private readonly TextBox _statsPriceTo;

	private readonly TextBox _statsPricePerSqFrom;

	private readonly TextBox _statsPricePerSqTo;

	private readonly TextBox _statsAreaFrom;

	private readonly TextBox _statsAreaTo;

	private readonly TextBox _statsAreaLotFrom;

	private readonly TextBox _statsAreaLotTo;

	private readonly DatePicker _statsFromDate;

	private readonly DatePicker _statsToDate;

	private readonly TextBlock _statsResultCount;

	private readonly ComboBox _statsPageSizeCombo;

	private readonly TextBlock _statsPageInfo;

	private readonly StackPanel _statsPaginationPanel;

	private readonly ItemsControl _statsResultsList;

	private readonly Border _statsSummaryPanel;

	private readonly Button _summaryMaxPrice;

	private readonly Button _summaryMinPrice;

	private readonly TextBlock _summaryAvgPrice;

	private readonly TextBlock _summaryAvgPricePerSq;

	private readonly Button _summaryShowPerSqTrend;

	private readonly Button _summaryShowTrend;

	private readonly Control _advancedSearchPanel;

	private readonly Button _advancedSearchToggleButton;

	private readonly ObservableCollection<StatsEntry> _statsEntries = new ObservableCollection<StatsEntry>();

	private readonly ObservableCollection<string> _objectOptions = new ObservableCollection<string>();

	private readonly ObservableCollection<string> _cityOptions = new ObservableCollection<string>();

	private readonly ObservableCollection<string> _addressOptions = new ObservableCollection<string>();
	private readonly ObservableCollection<string> _microDistrictOptions = new ObservableCollection<string>();

	private readonly ObservableCollection<string> _roomsOptions = new ObservableCollection<string>();

	private readonly ObservableCollection<string> _houseStateOptions = new ObservableCollection<string>();

	private CancellationTokenSource? _captureCts;

	private CancellationTokenSource? _refreshCts;

	private bool _isLoading;

	private bool _isBrowserReady;

	private bool _pointerPressed;

	private bool _isStatsLoading;

	private DateTime _lastPointerMove = DateTime.MinValue;

	private string? _summaryMaxUrl;

	private string? _summaryMinUrl;

	private IReadOnlyList<StatsListing> _lastQueriedStats = Array.Empty<StatsListing>();

	private bool _suppressFilterSelection;

	private int _statsPageSize = 200;

	private int _statsPageIndex = 1;

	private int _statsTotalCount;

	private bool _showOnlyWithoutHistory;

	private bool _showOnlyFavorites;

	private bool _showOnlyPriceDrop;

	private bool _showOnlyPriceIncrease;

	private bool _sortByPriceDescending;

	private bool _sortByPriceAscending;

	private bool _isAdvancedSearchVisible;

	public MainWindow()
	{
		InitializeComponent();
		_logService = App.GetRequiredService<ILogService>();
		_configurationService = App.GetRequiredService<IConfigurationService>();
		_playwrightRunner = App.GetRequiredService<IPlaywrightRunner>();
		_automationService = App.GetRequiredService<IAruodasAutomationService>();
		_databaseService = App.GetRequiredService<IDatabaseService>();
		_previewImage = this.FindControl<Image>("AruodasPreview");
		_previewPlaceholder = this.FindControl<Control>("PreviewPlaceholder");
		_aruodasButton = this.FindControl<Button>("AruodasButton");
		_statsButton = this.FindControl<Button>("StatsButton");
		_contentRoot = this.FindControl<Grid>("ContentRoot");
		_busyOverlay = this.FindControl<Border>("BusyOverlay");
		_browserSurface = this.FindControl<Control>("BrowserSurface");
		_statsContainer = this.FindControl<Control>("StatsContainer");
		_busyTitle = this.FindControl<TextBlock>("BusyTitle");
		_busyDetails = this.FindControl<TextBlock>("BusyDetails");
		_continueButton = this.FindControl<Button>("ContinueButton");
		_statsObjectCombo = this.FindControl<ComboBox>("StatsObjectCombo");
		_statsCityCombo = this.FindControl<ComboBox>("StatsCityCombo");
		_statsMicroDistrictCombo = this.FindControl<ComboBox>("StatsMicroDistrictCombo");
		_statsAddressCombo = this.FindControl<ComboBox>("StatsAddressCombo");
		_statsRoomsCombo = this.FindControl<ComboBox>("StatsRoomsCombo");
		_statsHouseStateCombo = this.FindControl<ComboBox>("StatsHouseStateCombo");
		_statsPriceFrom = this.FindControl<TextBox>("StatsPriceFrom");
		_statsPriceTo = this.FindControl<TextBox>("StatsPriceTo");
		_statsPricePerSqFrom = this.FindControl<TextBox>("StatsPricePerSqFrom");
		_statsPricePerSqTo = this.FindControl<TextBox>("StatsPricePerSqTo");
		_statsAreaFrom = this.FindControl<TextBox>("StatsAreaFrom");
		_statsAreaTo = this.FindControl<TextBox>("StatsAreaTo");
		_statsAreaLotFrom = this.FindControl<TextBox>("StatsAreaLotFrom");
		_statsAreaLotTo = this.FindControl<TextBox>("StatsAreaLotTo");
		_statsFromDate = this.FindControl<DatePicker>("StatsFromDate");
		_statsToDate = this.FindControl<DatePicker>("StatsToDate");
		_statsResultCount = this.FindControl<TextBlock>("StatsResultCount");
		_statsPageSizeCombo = this.FindControl<ComboBox>("StatsPageSizeCombo");
		_statsPageInfo = this.FindControl<TextBlock>("StatsPageInfo");
		_statsPaginationPanel = this.FindControl<StackPanel>("StatsPaginationPanel");
		_statsResultsList = this.FindControl<ItemsControl>("StatsResultsList");
		_statsResultsList.ItemsSource = _statsEntries;
		_statsObjectCombo.ItemsSource = _objectOptions;
		_statsCityCombo.ItemsSource = _cityOptions;
		_statsMicroDistrictCombo.ItemsSource = _microDistrictOptions;
		_statsAddressCombo.ItemsSource = _addressOptions;
		_statsRoomsCombo.ItemsSource = _roomsOptions;
		_statsHouseStateCombo.ItemsSource = _houseStateOptions;
		_statsSummaryPanel = this.FindControl<Border>("StatsSummaryPanel");
		_summaryMaxPrice = this.FindControl<Button>("SummaryMaxPrice");
		_summaryMinPrice = this.FindControl<Button>("SummaryMinPrice");
		_summaryAvgPrice = this.FindControl<TextBlock>("SummaryAvgPrice");
		_summaryAvgPricePerSq = this.FindControl<TextBlock>("SummaryAvgPricePerSq");
		_summaryShowPerSqTrend = this.FindControl<Button>("SummaryShowPerSqTrend");
		_summaryShowTrend = this.FindControl<Button>("SummaryShowTrend");
		_advancedSearchPanel = this.FindControl<Control>("AdvancedSearchPanel");
		_advancedSearchToggleButton = this.FindControl<Button>("AdvancedSearchToggleButton");
		ApplyWindowPreferences();
		ResetStatsPaging();
		ToggleAdvancedSearch(isVisible: false);
		_playwrightRunner.SearchTriggered += OnSearchTriggered;
		_automationService.AutomationCompleted += OnAutomationCompleted;
		_logService.Info("Pagrindinis langas inicializuotas.");
		base.Opened += delegate
		{
			_logService.Info("Langas atidarytas.");
		};
	}

	private void ApplyWindowPreferences()
	{
		WindowSettings window = _configurationService.Current.Window;
		base.Title = window.Title;
		base.Width = window.Width;
		base.Height = window.Height;
		base.MinWidth = window.MinWidth;
		base.MinHeight = window.MinHeight;
		base.CanResize = window.CanResize;
		base.WindowStartupLocation = ((!Enum.TryParse<WindowStartupLocation>(window.StartupLocation, out var result)) ? WindowStartupLocation.CenterScreen : result);
		if (Enum.TryParse<WindowState>(window.State, out var result2))
		{
			base.WindowState = result2;
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		_playwrightRunner.SearchTriggered -= OnSearchTriggered;
		_automationService.AutomationCompleted -= OnAutomationCompleted;
		_browserLifetimeCts.Cancel();
		_browserLifetimeCts.Dispose();
		_captureCts?.Cancel();
		_captureCts?.Dispose();
		_refreshCts?.Cancel();
		_refreshCts?.Dispose();
		Task.Run(async delegate
		{
			try
			{
				await _playwrightRunner.DisposeAsync();
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko uzdaryti Playwright resursu.", ex3);
			}
			finally
			{
				_logService.Info("Programa uzdaryta.");
				Environment.Exit(0);
			}
		});
	}

	private async void OnAruodasButtonClick(object? sender, RoutedEventArgs e)
	{
		if (_isLoading)
		{
			return;
		}
		_isLoading = true;
		_logService.Info("Paspaustas mygtukas: Aruodas.lt.");
		_aruodasButton.IsEnabled = false;
		_statsButton.IsEnabled = false;
		_previewPlaceholder.IsVisible = true;
		_previewImage.IsVisible = false;
		_statsContainer.IsVisible = false;
		_statsSummaryPanel.IsVisible = false;
		_browserSurface.IsVisible = true;
		_captureCts?.Cancel();
		_captureCts = CancellationTokenSource.CreateLinkedTokenSource(_browserLifetimeCts.Token);
		try
		{
			_logService.Info("Pradedamas aruodas.lt įkėlimas Playwright pagalba.");
			int width = 1200;
			int height = 1800;
			await UpdateImageAsync(await _playwrightRunner.NavigateAndCaptureAsync("https://www.aruodas.lt", width, height, _captureCts.Token));
			_isBrowserReady = true;
			_browserSurface.Focus();
			_logService.Info("Aruodas.lt interaktyvi peržiūra paruošta.");
		}
		catch (OperationCanceledException)
		{
			_logService.Info("Aruodas.lt įkėlimas nutrauktas.");
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			Exception ex4 = ex3;
			_previewPlaceholder.IsVisible = true;
			_logService.Error("Nepavyko inicializuoti Aruodas.lt peržiūros.", ex4);
		}
		finally
		{
			_aruodasButton.IsEnabled = true;
			_statsButton.IsEnabled = true;
			_isLoading = false;
		}
	}

	private async Task UpdateImageAsync(byte[] bytes)
	{
		await Dispatcher.UIThread.InvokeAsync(delegate
		{
			using MemoryStream stream = new MemoryStream(bytes);
			Bitmap bitmap = new Bitmap(stream);
			int width = bitmap.PixelSize.Width;
			int height = bitmap.PixelSize.Height;
			_browserSurface.Width = width;
			_browserSurface.Height = height;
			_previewImage.Width = width;
			_previewImage.Height = height;
			_previewImage.Source = bitmap;
			_previewImage.IsVisible = true;
			_previewPlaceholder.IsVisible = false;
		});
	}

	private async Task RefreshPreviewAsync()
	{
		if (!_isBrowserReady)
		{
			return;
		}
		try
		{
			await UpdateImageAsync(await _playwrightRunner.CaptureAsync(_browserLifetimeCts.Token));
		}
		catch (OperationCanceledException)
		{
		}
	}

	private void RequestPreviewRefresh()
	{
		_refreshCts?.Cancel();
		_refreshCts = CancellationTokenSource.CreateLinkedTokenSource(_browserLifetimeCts.Token);
		CancellationToken token = _refreshCts.Token;
		Task.Run(async delegate
		{
			try
			{
				await Task.Delay(200, token);
				await RefreshPreviewAsync();
			}
			catch (OperationCanceledException)
			{
			}
		}, token);
	}

	private bool TryTranslate(Point point, out (double X, double Y) translated)
	{
		Rect bounds = _browserSurface.Bounds;
		if (bounds.Width <= 0.0 || bounds.Height <= 0.0)
		{
			translated = default((double, double));
			return false;
		}
		double num = point.X / bounds.Width;
		double num2 = point.Y / bounds.Height;
		translated = (X: num * (double)_playwrightRunner.ViewportWidth, Y: num2 * (double)_playwrightRunner.ViewportHeight);
		return true;
	}

	private async void OnBrowserPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (_isBrowserReady)
		{
			Point point = e.GetPosition(_browserSurface);
			if (TryTranslate(point, out var translated))
			{
				_browserSurface.Focus();
				_pointerPressed = true;
				RpaAruodas.Services.MouseButton button = GetMouseButton(e);
				await _playwrightRunner.MouseDownAsync(translated.X, translated.Y, button, _browserLifetimeCts.Token);
				e.Handled = true;
				RequestPreviewRefresh();
			}
		}
	}

	private async void OnBrowserPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (_isBrowserReady && _pointerPressed)
		{
			Point point = e.GetPosition(_browserSurface);
			if (TryTranslate(point, out var translated))
			{
				RpaAruodas.Services.MouseButton button = GetMouseButton(e);
				await _playwrightRunner.MouseUpAsync(translated.X, translated.Y, button, _browserLifetimeCts.Token);
				_pointerPressed = false;
				e.Handled = true;
				RequestPreviewRefresh();
			}
		}
	}

	private async void OnBrowserPointerMoved(object? sender, PointerEventArgs e)
	{
		if (!_isBrowserReady)
		{
			return;
		}
		DateTime now = DateTime.UtcNow;
		if (!_pointerPressed && now - _lastPointerMove < TimeSpan.FromMilliseconds(80.0))
		{
			return;
		}
		_lastPointerMove = now;
		Point point = e.GetPosition(_browserSurface);
		if (TryTranslate(point, out var translated))
		{
			await _playwrightRunner.MouseMoveAsync(translated.X, translated.Y, _browserLifetimeCts.Token);
			if (_pointerPressed)
			{
				RequestPreviewRefresh();
			}
		}
	}

	private async void OnBrowserPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		if (_isBrowserReady)
		{
			await _playwrightRunner.MouseWheelAsync(e.Delta.X * 120.0, (0.0 - e.Delta.Y) * 120.0, _browserLifetimeCts.Token);
			e.Handled = true;
			RequestPreviewRefresh();
		}
	}

	private async void OnBrowserTextInput(object? sender, TextInputEventArgs e)
	{
		if (_isBrowserReady && !string.IsNullOrEmpty(e.Text))
		{
			await _playwrightRunner.TypeTextAsync(e.Text, _browserLifetimeCts.Token);
			e.Handled = true;
			RequestPreviewRefresh();
		}
	}

	private async void OnBrowserKeyDown(object? sender, KeyEventArgs e)
	{
		if (_isBrowserReady)
		{
			string key = MapSpecialKey(e.Key);
			if (key != null)
			{
				await _playwrightRunner.PressKeyAsync(key, _browserLifetimeCts.Token);
				e.Handled = true;
				RequestPreviewRefresh();
			}
		}
	}

	private void OnSearchTriggered(object? sender, EventArgs e)
	{
		ShowBusyOverlay();
	}

	private void OnAutomationCompleted(object? sender, AutomationCompletedEventArgs e)
	{
		ShowCompletedOverlay(e);
	}

	private void ShowBusyOverlay()
	{
		Dispatcher.UIThread.Post(delegate
		{
			_busyOverlay.IsVisible = true;
			_contentRoot.IsEnabled = false;
			_contentRoot.Effect = new BlurEffect
			{
				Radius = 10.0
			};
			_busyTitle.Text = "Renkama informacija";
			_busyDetails.Text = "Vykdomas paieskos ir duomenu rinkimo procesas.";
			_continueButton.IsVisible = false;
			_continueButton.IsEnabled = false;
			_continueButton.Opacity = 0.8;
		});
	}

	private void ShowCompletedOverlay(AutomationCompletedEventArgs e)
	{
		Dispatcher.UIThread.Post(delegate
		{
			_busyOverlay.IsVisible = true;
			_contentRoot.IsEnabled = false;
			_contentRoot.Effect = new BlurEffect
			{
				Radius = 10.0
			};
			_busyTitle.Text = (e.Success ? "Rinkimas baigtas" : "Rinkimas nutruktas");
			_busyDetails.Text = $"Rasta skelbimu: {e.Found}\nSurinkta informacija: {e.Collected}\nIrasyta i DB: {e.Inserted} (praleista: {e.Skipped})" + (string.IsNullOrWhiteSpace(e.ErrorMessage) ? string.Empty : ("\nKlaida: " + e.ErrorMessage));
			_continueButton.IsVisible = true;
			_continueButton.IsEnabled = true;
			_continueButton.Opacity = 1.0;
		});
	}

	private void OnContinueButtonClick(object? sender, RoutedEventArgs e)
	{
		_busyOverlay.IsVisible = false;
		_contentRoot.IsEnabled = true;
		_contentRoot.Effect = null;
		_continueButton.IsVisible = false;
		_busyDetails.Text = string.Empty;
		_logService.Info("Vartotojas tęsia darbą po RPA pabaigos.");
	}

	private async void OnStatsButtonClick(object? sender, RoutedEventArgs e)
	{
		if (_isLoading)
		{
			return;
		}
		_isLoading = true;
		_logService.Info("Paspaustas mygtukas: Statistika.");
		_aruodasButton.IsEnabled = false;
		_statsButton.IsEnabled = false;
		_captureCts?.Cancel();
		_refreshCts?.Cancel();
		try
		{
			await _playwrightRunner.CloseAsync();
			_isBrowserReady = false;
			_pointerPressed = false;
			_previewImage.IsVisible = false;
			_previewPlaceholder.IsVisible = false;
			_browserSurface.IsVisible = false;
			_statsContainer.IsVisible = true;
			await ResetStatsUiAsync(reloadFilters: true, collapseAdvanced: true);
			_logService.Info("Perjungta i statistikos vaizda. Chromium uzdarytas.");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko parodyti statistikos lango.", ex3);
		}
		finally
		{
			_aruodasButton.IsEnabled = true;
			_statsButton.IsEnabled = true;
			_isLoading = false;
		}
	}

	private static string? MapSpecialKey(Key key)
	{
		bool flag = false;
		if (1 == 0)
		{
		}
		string text = key switch
		{
			Key.Return => "Enter", 
			Key.Tab => "Tab", 
			Key.Back => "Backspace", 
			Key.Delete => "Delete", 
			Key.Left => "ArrowLeft", 
			Key.Right => "ArrowRight", 
			Key.Up => "ArrowUp", 
			Key.Down => "ArrowDown", 
			Key.PageUp => "PageUp", 
			Key.PageDown => "PageDown", 
			Key.Home => "Home", 
			Key.End => "End", 
			Key.Escape => "Escape", 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		string result = text;
		bool flag2 = false;
		return result;
	}

	private RpaAruodas.Services.MouseButton GetMouseButton(PointerEventArgs e)
	{
		PointerPointProperties properties = e.GetCurrentPoint(_browserSurface).Properties;
		if (properties.IsRightButtonPressed || e is PointerReleasedEventArgs { InitialPressMouseButton: Avalonia.Input.MouseButton.Right })
		{
			return RpaAruodas.Services.MouseButton.Right;
		}
		if (properties.IsMiddleButtonPressed || e is PointerReleasedEventArgs { InitialPressMouseButton: Avalonia.Input.MouseButton.Middle })
		{
			return RpaAruodas.Services.MouseButton.Middle;
		}
		return RpaAruodas.Services.MouseButton.Left;
	}

	private async void OnStatsFilterClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Filtruoti.");
		_showOnlyWithoutHistory = false;
		_showOnlyFavorites = false;
		_showOnlyPriceDrop = false;
		_showOnlyPriceIncrease = false;
		await EnsureHouseStateOptionsAsync();
		_statsPageIndex = 1;
		UpdatePaginationUi();
		await LoadStatsAsync();
	}

	private async Task ToggleFavoriteAsync(StatsEntry entry)
	{
		if (!string.IsNullOrWhiteSpace(entry.ExternalId))
		{
			bool newValue = !entry.Selected;
			await _databaseService.SetSelectedAsync(entry.ExternalId, entry.SearchObject, newValue, CancellationToken.None);
			StatsListing updatedListing = entry.ToListingWithSelected(newValue);
			entry.SetSelected(newValue);
			int index = _statsEntries.IndexOf(entry);
			if (index >= 0)
			{
				_statsEntries[index] = new StatsEntry(updatedListing);
			}
		}
	}

	private async void OnStatsItemPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		StatsEntry entry = null;
		int num;
		if (sender is Border border)
		{
			object dataContext = border.DataContext;
			entry = dataContext as StatsEntry;
			num = ((entry != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		if (num != 0)
		{
			object dataContext2 = e.Source;
			if (!(dataContext2 is Control control) || control.FindAncestorOfType<Button>() == null)
			{
				await ToggleFavoriteAsync(entry);
			}
		}
	}

	private async void OnStatsResetClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Isvalyti.");
		await ResetStatsUiAsync(reloadFilters: true, collapseAdvanced: true);
	}

	private async void OnShowLatestClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Rodyti naujausius.");
		_sortByPriceDescending = false;
		_sortByPriceAscending = false;
		_showOnlyWithoutHistory = true;
		_showOnlyFavorites = false;
		_showOnlyPriceDrop = false;
		_showOnlyPriceIncrease = false;
		_statsPageIndex = 1;
		UpdatePaginationUi();
		await LoadStatsAsync();
	}

	private void OnShowMarkedClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Rodyti pazymetus.");
		_showOnlyFavorites = true;
		_showOnlyWithoutHistory = false;
		_showOnlyPriceDrop = false;
		_showOnlyPriceIncrease = false;
		_sortByPriceDescending = false;
		_sortByPriceAscending = false;
		_statsPageIndex = 1;
		UpdatePaginationUi();
		LoadStatsAsync();
	}

	private async void OnShowPriceDropClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Rodyti atpigusius skelbimus.");
		_showOnlyPriceDrop = true;
		_showOnlyFavorites = false;
		_showOnlyWithoutHistory = false;
		_showOnlyPriceIncrease = false;
		_sortByPriceDescending = false;
		_sortByPriceAscending = false;
		_statsPageIndex = 1;
		UpdatePaginationUi();
		await LoadStatsAsync();
	}

	private async void OnShowPriceIncreaseClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Rodyti pabrangusius skelbimus.");
		_showOnlyPriceIncrease = true;
		_showOnlyPriceDrop = false;
		_showOnlyFavorites = false;
		_showOnlyWithoutHistory = false;
		_sortByPriceDescending = false;
		_sortByPriceAscending = false;
		_statsPageIndex = 1;
		UpdatePaginationUi();
		await LoadStatsAsync();
	}

	private async void OnSortByHighestPriceClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Filtruoti pagal brangiausia.");
		_sortByPriceDescending = true;
		_sortByPriceAscending = false;
		_statsPageIndex = 1;
		UpdatePaginationUi();
		await LoadStatsAsync();
	}

	private async void OnSortByLowestPriceClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Filtruoti pagal pigiausia.");
		_sortByPriceAscending = true;
		_sortByPriceDescending = false;
		_statsPageIndex = 1;
		UpdatePaginationUi();
		await LoadStatsAsync();
	}

	private async void OnCompareStatsClick(object? sender, RoutedEventArgs e)
	{
		_logService.Info("Paspaustas mygtukas: Palyginti statistika.");
		CompareStatsWindow dialog = new CompareStatsWindow(_databaseService, _logService);
		dialog.Show(this);
	}

	private async void OnStatsPageSizeChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_statsPageSizeCombo != null)
		{
			int selectedSize = GetSelectedPageSize();
			if (selectedSize == _statsPageSize && _statsTotalCount == 0)
			{
				UpdatePaginationUi();
				return;
			}
			_statsPageSize = selectedSize;
			_statsPageIndex = 1;
			UpdatePaginationUi();
			await LoadStatsAsync();
		}
	}

	private async void OnPaginationButtonClick(object? sender, RoutedEventArgs e)
	{
		int targetPage = 0;
		int num;
		if (sender is Button button)
		{
			object tag = button.Tag;
			if (tag is int)
			{
				targetPage = (int)tag;
				num = 1;
				goto IL_0087;
			}
		}
		num = 0;
		goto IL_0087;
		IL_0087:
		if (num != 0)
		{
			int totalPages = GetTotalPages();
			int clamped = Math.Clamp(targetPage, 1, totalPages);
			if (clamped != _statsPageIndex)
			{
				_statsPageIndex = clamped;
				UpdatePaginationUi();
				await LoadStatsAsync();
			}
		}
	}

	private async void OnStatsCitySelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFilterSelection)
		{
			string city = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			string room = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			await EnsureObjectOptionsAsync(city);
			string obj = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			_logService.Info("Pasirinktas miestas: " + (city ?? "-"));
			await EnsureAddressOptionsAsync(obj, city, room);
			_statsAddressCombo.SelectedIndex = -1;
			await EnsureRoomOptionsAsync(obj, city, null);
			await EnsureHouseStateOptionsAsync();
			string updatedRoom = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			await EnsureMicroDistrictOptionsAsync(obj, city, updatedRoom);
			UpdateAreaWatermarksAsync();
		}
	}

	private async void OnStatsObjectSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFilterSelection)
		{
			string obj = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			string city = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			string address = NormalizeSelection(_statsAddressCombo.SelectedItem as string);
			_logService.Info("Pasirinktas objekto tipas: " + (obj ?? "-"));
			await EnsureCityOptionsAsync(obj);
			await EnsureRoomOptionsAsync(obj, city, address);
			_statsRoomsCombo.SelectedIndex = -1;
			string room = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			await EnsureAddressOptionsAsync(obj, city, room);
			await EnsureHouseStateOptionsAsync();
			await EnsureMicroDistrictOptionsAsync(obj, city, room);
			UpdateAreaWatermarksAsync();
		}
	}

	private async void OnStatsAddressSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFilterSelection)
		{
			string obj = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			string city = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			string address = NormalizeSelection(_statsAddressCombo.SelectedItem as string);
			_logService.Info($"Pasirinktas adresas: {address ?? "-"} (miestas: {city ?? "-"})");
			await EnsureRoomOptionsAsync(obj, city, address);
			await EnsureHouseStateOptionsAsync();
			UpdateAreaWatermarksAsync();
		}
	}

	private async void OnStatsRoomsSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFilterSelection)
		{
			string room = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			string city = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			string obj = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			_logService.Info($"Pasirinkti kambariai: {room ?? "-"} (miestas: {city ?? "-"}, objektas: {obj ?? "-"})");
			await EnsureHouseStateOptionsAsync();
			await EnsureMicroDistrictOptionsAsync(obj, city, room);
			UpdateAreaWatermarksAsync();
		}
	}

	private async void OnStatsMicroDistrictSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFilterSelection)
		{
			string? microDistrict = NormalizeSelection(_statsMicroDistrictCombo.SelectedItem as string);
			_logService.Info("Pasirinktas mikrorajonas: " + (microDistrict ?? "-"));
			await EnsureHouseStateOptionsAsync();
			UpdateAreaWatermarksAsync();
		}
	}

	private void OnStatsHouseStateSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFilterSelection)
		{
			string text = NormalizeSelection(_statsHouseStateCombo.SelectedItem as string);
			_logService.Info("Pasirinkta būklė: " + (text ?? "-"));
			UpdateAreaWatermarksAsync();
		}
	}

	private async Task EnsureStatsFiltersAsync()
	{
		try
		{
			IReadOnlyList<string> cities = await _databaseService.GetDistinctSearchCitiesAsync(CancellationToken.None);
			IReadOnlyList<string> rooms = await _databaseService.GetDistinctRoomsAsync(null, null, null, CancellationToken.None);
			_cityOptions.Clear();
			_roomsOptions.Clear();
			_houseStateOptions.Clear();
			_cityOptions.Add(string.Empty);
			_roomsOptions.Add(string.Empty);
			_houseStateOptions.Add(string.Empty);
			foreach (string city in cities)
			{
				_cityOptions.Add(city);
			}
			foreach (string room in rooms)
			{
				_roomsOptions.Add(room);
			}
			await EnsureObjectOptionsAsync(null);
			await EnsureCityOptionsAsync(null);
			await EnsureAddressOptionsAsync(null, null, null);
			await EnsureHouseStateOptionsAsync();
			await EnsureMicroDistrictOptionsAsync(null, null, null);
			UpdateAreaWatermarksAsync();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko uzkrauti filtru sarasu.", ex3);
		}
	}

	private int GetSelectedPageSize()
	{
		object obj = _statsPageSizeCombo?.SelectedItem;
		if (obj is ComboBoxItem comboBoxItem && int.TryParse(comboBoxItem.Content?.ToString(), out var result))
		{
			return Math.Max(1, result);
		}
		if (obj is string s && int.TryParse(s, out var result2))
		{
			return Math.Max(1, result2);
		}
		return Math.Max(1, _statsPageSize);
	}

	private int GetTotalPages()
	{
		int num = Math.Max(1, _statsPageSize);
		return (_statsTotalCount <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling((double)_statsTotalCount / (double)num));
	}

	private void ResetStatsPaging()
	{
		_statsPageIndex = 1;
		_statsTotalCount = 0;
		UpdatePaginationUi();
	}

	private void SetStatsLoading(bool isLoading)
	{
		_isStatsLoading = isLoading;
	}

	private void ToggleAdvancedSearch(bool isVisible)
	{
		_isAdvancedSearchVisible = isVisible;
		_advancedSearchPanel.IsVisible = isVisible;
		_advancedSearchToggleButton.Content = (isVisible ? "Slėpti išplėstinę paiešką" : "Išplėstinė paieška");
	}

	private void OnAdvancedSearchToggleClick(object? sender, RoutedEventArgs e)
	{
		ToggleAdvancedSearch(!_isAdvancedSearchVisible);
	}

	private async Task ResetStatsUiAsync(bool reloadFilters, bool collapseAdvanced = false)
	{
		_statsObjectCombo.SelectedIndex = -1;
		_statsCityCombo.SelectedIndex = -1;
		_statsMicroDistrictCombo.SelectedIndex = -1;
		_statsAddressCombo.SelectedIndex = -1;
		_statsRoomsCombo.SelectedIndex = -1;
		_statsHouseStateCombo.SelectedIndex = -1;
		_statsFromDate.SelectedDate = null;
		_statsToDate.SelectedDate = null;
		_statsPriceFrom.Text = string.Empty;
		_statsPriceTo.Text = string.Empty;
		_statsPricePerSqFrom.Text = string.Empty;
		_statsPricePerSqTo.Text = string.Empty;
		_statsAreaFrom.Text = string.Empty;
		_statsAreaTo.Text = string.Empty;
		_statsAreaLotFrom.Text = string.Empty;
		_statsAreaLotTo.Text = string.Empty;
		_statsEntries.Clear();
		_statsResultCount.Text = "Rezultatai: 0";
		_statsSummaryPanel.IsVisible = false;
		_summaryShowPerSqTrend.IsVisible = false;
		_summaryShowTrend.IsVisible = false;
		_statsTotalCount = 0;
		_lastQueriedStats = Array.Empty<StatsListing>();
		_showOnlyWithoutHistory = false;
		_showOnlyFavorites = false;
		_showOnlyPriceDrop = false;
		_showOnlyPriceIncrease = false;
		_sortByPriceDescending = false;
		_sortByPriceAscending = false;
		ResetStatsPaging();
		if (collapseAdvanced)
		{
			ToggleAdvancedSearch(isVisible: false);
		}
		if (reloadFilters)
		{
			await EnsureStatsFiltersAsync();
		}
		UpdateAreaWatermarksAsync();
	}

	private void UpdatePaginationUi()
	{
		if (_statsPageSizeCombo != null && _statsPageInfo != null && _statsPaginationPanel != null)
		{
			_statsPageSize = GetSelectedPageSize();
			int totalPages = GetTotalPages();
			_statsPageIndex = Math.Clamp(_statsPageIndex, 1, totalPages);
			_statsPageInfo.Text = $"Puslapis {_statsPageIndex} / {totalPages}";
			BuildPaginationButtons(totalPages);
		}
	}

	private void BuildPaginationButtons(int totalPages)
	{
		_statsPaginationPanel.Children.Clear();
		if (totalPages <= 1 || _statsTotalCount == 0)
		{
			return;
		}
		SolidColorBrush normalBackground = new SolidColorBrush(Color.FromRgb(211, 211, 211));
		SolidColorBrush activeBackground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
		IImmutableSolidColorBrush normalForeground = Brushes.Black;
		IImmutableSolidColorBrush activeForeground = Brushes.White;
		SolidColorBrush normalBorder = new SolidColorBrush(Color.FromRgb(176, 176, 176));
		SolidColorBrush activeBorder = new SolidColorBrush(Color.FromRgb(48, 48, 48));
		_statsPaginationPanel.Children.Add(CreateButton("«", 1, _statsPageIndex > 1));
		_statsPaginationPanel.Children.Add(CreateButton("‹", Math.Max(1, _statsPageIndex - 1), _statsPageIndex > 1));
		if (totalPages <= 9)
		{
			for (int i = 1; i <= totalPages; i++)
			{
				_statsPaginationPanel.Children.Add(CreateButton(i.ToString(), i, i != _statsPageIndex, i == _statsPageIndex));
			}
		}
		else
		{
			_statsPaginationPanel.Children.Add(CreateButton("1", 1, _statsPageIndex != 1, _statsPageIndex == 1));
			int num = Math.Max(2, _statsPageIndex - 2);
			int num2 = Math.Min(totalPages - 1, _statsPageIndex + 2);
			if (num > 2)
			{
				AddEllipsis();
			}
			else
			{
				num = 2;
			}
			for (int j = num; j <= num2; j++)
			{
				_statsPaginationPanel.Children.Add(CreateButton(j.ToString(), j, j != _statsPageIndex, j == _statsPageIndex));
			}
			if (num2 < totalPages - 1)
			{
				AddEllipsis();
			}
			_statsPaginationPanel.Children.Add(CreateButton(totalPages.ToString(), totalPages, _statsPageIndex != totalPages, _statsPageIndex == totalPages));
		}
		_statsPaginationPanel.Children.Add(CreateButton("›", Math.Min(totalPages, _statsPageIndex + 1), _statsPageIndex < totalPages));
		_statsPaginationPanel.Children.Add(CreateButton("»", totalPages, _statsPageIndex < totalPages));
		void AddEllipsis()
		{
			_statsPaginationPanel.Children.Add(new TextBlock
			{
				Text = "...",
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(4.0, 0.0, 4.0, 0.0),
				Foreground = Brushes.Gray
			});
		}
		Button CreateButton(string content, int targetPage, bool isClickable, bool isActive = false)
		{
			Button button = new Button
			{
				Content = content,
				Padding = new Thickness(10.0, 6.0),
				MinWidth = 38.0,
				Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
				Background = (isActive ? activeBackground : normalBackground),
				Foreground = (isActive ? activeForeground : normalForeground),
				BorderBrush = (isActive ? activeBorder : normalBorder),
				BorderThickness = new Thickness(1.0),
				FontWeight = (isActive ? FontWeight.DemiBold : FontWeight.Normal),
				IsEnabled = (isActive || isClickable),
				IsHitTestVisible = isClickable
			};
			button.Tag = targetPage;
			button.Click += OnPaginationButtonClick;
			return button;
		}
	}

	private async Task LoadStatsAsync()
	{
		SetStatsLoading(isLoading: true);
		try
		{
			_statsPageSize = GetSelectedPageSize();
			int offset = (_statsPageIndex - 1) * _statsPageSize;
			DateTime? fromDate = _statsFromDate.SelectedDate?.DateTime.Date;
			DateTime? toDate = _statsToDate.SelectedDate?.DateTime.Date;
			double? priceFrom = ParseNullableDouble(_statsPriceFrom.Text);
			double? priceTo = ParseNullableDouble(_statsPriceTo.Text);
			double? pricePerSqFrom = ParseNullableDouble(_statsPricePerSqFrom.Text);
			double? pricePerSqTo = ParseNullableDouble(_statsPricePerSqTo.Text);
			double? areaFrom = ParseNullableDouble(_statsAreaFrom.Text);
			double? areaTo = ParseNullableDouble(_statsAreaTo.Text);
			double? areaLotFrom = ParseNullableDouble(_statsAreaLotFrom.Text);
			double? areaLotTo = ParseNullableDouble(_statsAreaLotTo.Text);
			string address = NormalizeSelection(_statsAddressCombo.SelectedItem as string);
			string rooms = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			string houseState = NormalizeSelection(_statsHouseStateCombo.SelectedItem as string);
			string? microDistrict = NormalizeSelection(_statsMicroDistrictCombo.SelectedItem as string);
			StatsQueryResult result = await _databaseService.QueryListingsAsync(_statsObjectCombo.SelectedItem as string, _statsCityCombo.SelectedItem as string, microDistrict, address, fromDate, toDate, priceFrom, priceTo, pricePerSqFrom, pricePerSqTo, areaFrom, areaTo, areaLotFrom, areaLotTo, rooms, houseState, _statsPageSize, Math.Max(0, offset), _showOnlyWithoutHistory, _showOnlyFavorites, _showOnlyPriceDrop, _showOnlyPriceIncrease, _sortByPriceDescending, _sortByPriceAscending, CancellationToken.None);
			_statsTotalCount = Math.Max(result.TotalCount, result.Listings.Count);
			int totalPages = GetTotalPages();
			if (_statsPageIndex > totalPages && totalPages > 0)
			{
				_statsPageIndex = totalPages;
				UpdatePaginationUi();
				return;
			}
			_statsEntries.Clear();
			foreach (StatsListing item in result.Listings)
			{
				_statsEntries.Add(new StatsEntry(item));
			}
			_lastQueriedStats = result.Listings.ToList();
			_statsResultCount.Text = $"Rezultatai: {_statsTotalCount}";
			UpdateStatsSummary(result.Listings, result.AveragePricePerSquare, result.MinPrice, result.MaxPrice, result.AveragePrice, result.MaxPriceUrl, result.MinPriceUrl);
			UpdatePaginationUi();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko užkrauti statistikos.", ex3);
			_statsResultCount.Text = "Rezultatai: klaida";
			_statsSummaryPanel.IsVisible = false;
			_summaryShowPerSqTrend.IsVisible = false;
			_summaryShowTrend.IsVisible = false;
			_lastQueriedStats = Array.Empty<StatsListing>();
			_statsTotalCount = 0;
			ResetStatsPaging();
		}
		finally
		{
			SetStatsLoading(isLoading: false);
		}
	}

	private static double? ParseNullableDouble(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string s = text.Replace("€", string.Empty).Replace("€/m²", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("€/m2", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("/m²", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("/m2", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("m²", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace("m2", string.Empty, StringComparison.OrdinalIgnoreCase)
			.Replace(" ", string.Empty)
			.Replace("\u00a0", string.Empty)
			.Replace(",", ".");
		double result;
		return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? new double?(result) : ((double?)null);
	}

	private (double? PriceFrom, double? PriceTo, double? PricePerSqFrom, double? PricePerSqTo, double? AreaFrom, double? AreaTo, double? AreaLotFrom, double? AreaLotTo) GetCurrentNumericFilters()
	{
		double? item = ParseNullableDouble(_statsPriceFrom.Text);
		double? item2 = ParseNullableDouble(_statsPriceTo.Text);
		double? item3 = ParseNullableDouble(_statsPricePerSqFrom.Text);
		double? item4 = ParseNullableDouble(_statsPricePerSqTo.Text);
		double? item5 = ParseNullableDouble(_statsAreaFrom.Text);
		double? item6 = ParseNullableDouble(_statsAreaTo.Text);
		double? item7 = ParseNullableDouble(_statsAreaLotFrom.Text);
		double? item8 = ParseNullableDouble(_statsAreaLotTo.Text);
		return (PriceFrom: item, PriceTo: item2, PricePerSqFrom: item3, PricePerSqTo: item4, AreaFrom: item5, AreaTo: item6, AreaLotFrom: item7, AreaLotTo: item8);
	}

	private static string? NormalizeSelection(string? value)
	{
		return (string.IsNullOrWhiteSpace(value) || value == "-") ? null : value;
	}

	private async Task EnsureAddressOptionsAsync(string? searchObject, string? city, string? rooms)
	{
		_suppressFilterSelection = true;
		try
		{
			_statsAddressCombo.SelectedIndex = -1;
			_addressOptions.Clear();
			_addressOptions.Add(string.Empty);
			if (string.IsNullOrWhiteSpace(city))
			{
				return;
			}
			try
			{
				foreach (string address in await _databaseService.GetDistinctAddressesAsync(searchObject, city, rooms, CancellationToken.None))
				{
					_addressOptions.Add(address);
				}
				_logService.Info($"Adresu sarasas atnaujintas (objektas: {searchObject ?? "-"}, miestas: {city ?? "-"}, kambariai: {rooms ?? "-"})");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko užkrauti adresų sarašo.", ex3);
			}
			EnsureValidSelection(_statsAddressCombo, _addressOptions.Count);
		}
		finally
		{
			_suppressFilterSelection = false;
		}
	}

	private async Task EnsureMicroDistrictOptionsAsync(string? searchObject, string? city, string? rooms)
	{
		_suppressFilterSelection = true;
		try
		{
			_statsMicroDistrictCombo.SelectedIndex = -1;
			_microDistrictOptions.Clear();
			_microDistrictOptions.Add(string.Empty);
			try
			{
				foreach (string microDistrict in await _databaseService.GetDistinctMicroDistrictsAsync(searchObject, city, rooms, CancellationToken.None))
				{
					_microDistrictOptions.Add(microDistrict);
				}
				_logService.Info($"Mikrorajonu sarasas atnaujintas (objektas: {searchObject ?? "-"}, miestas: {city ?? "-"}, kambariai: {rooms ?? "-"})");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko uzkrauti mikrorajonu saraso.", ex3);
			}
			EnsureValidSelection(_statsMicroDistrictCombo, _microDistrictOptions.Count);
		}
		finally
		{
			_suppressFilterSelection = false;
		}
	}

	private async Task EnsureHouseStateOptionsAsync()
	{
		_suppressFilterSelection = true;
		try
		{
			string obj = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			string city = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			string address = NormalizeSelection(_statsAddressCombo.SelectedItem as string);
			string rooms = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			string current = NormalizeSelection(_statsHouseStateCombo.SelectedItem as string);
			(double? PriceFrom, double? PriceTo, double? PricePerSqFrom, double? PricePerSqTo, double? AreaFrom, double? AreaTo, double? AreaLotFrom, double? AreaLotTo) ranges = GetCurrentNumericFilters();
			_statsHouseStateCombo.SelectedIndex = -1;
			_houseStateOptions.Clear();
			_houseStateOptions.Add(string.Empty);
			try
			{
				foreach (string state in await _databaseService.GetDistinctHouseStatesAsync(obj, city, address, rooms, ranges.PriceFrom, ranges.PriceTo, ranges.PricePerSqFrom, ranges.PricePerSqTo, ranges.AreaFrom, ranges.AreaTo, ranges.AreaLotFrom, ranges.AreaLotTo, CancellationToken.None))
				{
					_houseStateOptions.Add(state);
				}
				_logService.Info($"Bukles sarasas atnaujintas (objektas: {obj ?? "-"}, miestas: {city ?? "-"}, adresas: {address ?? "-"}, kambariai: {rooms ?? "-"})");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko uzkrauti bukles saraso.", ex3);
			}
			if (!string.IsNullOrWhiteSpace(current) && _houseStateOptions.Contains(current))
			{
				_statsHouseStateCombo.SelectedItem = current;
			}
			else
			{
				_statsHouseStateCombo.SelectedIndex = -1;
			}
		}
		finally
		{
			_suppressFilterSelection = false;
		}
	}

	private async Task EnsureRoomOptionsAsync(string? searchObject, string? searchCity, string? address)
	{
		_suppressFilterSelection = true;
		try
		{
			_statsRoomsCombo.SelectedIndex = -1;
			_roomsOptions.Clear();
			_roomsOptions.Add(string.Empty);
			try
			{
				foreach (string room in await _databaseService.GetDistinctRoomsAsync(searchObject, searchCity, address, CancellationToken.None))
				{
					_roomsOptions.Add(room);
				}
				_logService.Info($"Kambariu sarasas atnaujintas (objektas: {searchObject ?? "-"}, miestas: {searchCity ?? "-"}, adresas: {address ?? "-"})");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko uzkrauti kambariu saraso.", ex3);
			}
			EnsureValidSelection(_statsRoomsCombo, _roomsOptions.Count);
		}
		finally
		{
			_suppressFilterSelection = false;
		}
	}

	private async Task EnsureObjectOptionsAsync(string? searchCity)
	{
		_suppressFilterSelection = true;
		try
		{
			string current = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			_statsObjectCombo.SelectedIndex = -1;
			_objectOptions.Clear();
			_objectOptions.Add(string.Empty);
			try
			{
				foreach (string obj in await _databaseService.GetDistinctSearchObjectsAsync(searchCity, CancellationToken.None))
				{
					_objectOptions.Add(obj);
				}
				_logService.Info("Objektu sarasas atnaujintas (miestas: " + (searchCity ?? "-") + ")");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko uzkrauti objekto saraso.", ex3);
			}
			if (!string.IsNullOrWhiteSpace(current) && _objectOptions.Contains(current))
			{
				_statsObjectCombo.SelectedItem = current;
			}
			else
			{
				_statsObjectCombo.SelectedIndex = -1;
			}
		}
		finally
		{
			_suppressFilterSelection = false;
		}
	}

	private async Task EnsureCityOptionsAsync(string? searchObject)
	{
		_suppressFilterSelection = true;
		try
		{
			string current = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			_statsCityCombo.SelectedIndex = -1;
			_cityOptions.Clear();
			_cityOptions.Add(string.Empty);
			try
			{
				foreach (string city in await _databaseService.GetDistinctSearchCitiesAsync(searchObject, CancellationToken.None))
				{
					_cityOptions.Add(city);
				}
				_logService.Info("Miestu sarasas atnaujintas (objektas: " + (searchObject ?? "-") + ")");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				_logService.Error("Nepavyko uzkrauti miestu saraso.", ex3);
			}
			if (!string.IsNullOrWhiteSpace(current) && _cityOptions.Contains(current))
			{
				_statsCityCombo.SelectedItem = current;
			}
			else
			{
				_statsCityCombo.SelectedIndex = -1;
			}
		}
		finally
		{
			_suppressFilterSelection = false;
		}
	}

	private static void EnsureValidSelection(ComboBox combo, int itemCount)
	{
		if (combo.SelectedIndex >= itemCount || combo.SelectedIndex < -1)
		{
			combo.SelectedIndex = -1;
		}
	}

	private async Task UpdateAreaWatermarksAsync()
	{
		try
		{
			string obj = NormalizeSelection(_statsObjectCombo.SelectedItem as string);
			string city = NormalizeSelection(_statsCityCombo.SelectedItem as string);
			string? microDistrict = NormalizeSelection(_statsMicroDistrictCombo.SelectedItem as string);
			string address = NormalizeSelection(_statsAddressCombo.SelectedItem as string);
			string rooms = NormalizeSelection(_statsRoomsCombo.SelectedItem as string);
			string state = NormalizeSelection(_statsHouseStateCombo.SelectedItem as string);
			bool hasSelection = obj != null || city != null || address != null || rooms != null || state != null;
			bool hasTypedRanges = !string.IsNullOrWhiteSpace(_statsAreaFrom.Text) || !string.IsNullOrWhiteSpace(_statsAreaTo.Text) || !string.IsNullOrWhiteSpace(_statsAreaLotFrom.Text) || !string.IsNullOrWhiteSpace(_statsAreaLotTo.Text) || !string.IsNullOrWhiteSpace(_statsPriceFrom.Text) || !string.IsNullOrWhiteSpace(_statsPriceTo.Text) || !string.IsNullOrWhiteSpace(_statsPricePerSqFrom.Text) || !string.IsNullOrWhiteSpace(_statsPricePerSqTo.Text);
			if (!hasSelection && !hasTypedRanges)
			{
				_statsPriceFrom.Watermark = null;
				_statsPriceTo.Watermark = null;
				_statsPricePerSqFrom.Watermark = null;
				_statsPricePerSqTo.Watermark = null;
				_statsAreaFrom.Watermark = null;
				_statsAreaTo.Watermark = null;
				_statsAreaLotFrom.Watermark = null;
				_statsAreaLotTo.Watermark = null;
				return;
			}
			var (priceMin, priceMax) = await _databaseService.GetPriceBoundsAsync(obj, city, microDistrict, address, rooms, state, CancellationToken.None);
			var (pricePerMin, pricePerMax) = await _databaseService.GetPricePerSquareBoundsAsync(obj, city, microDistrict, address, rooms, state, CancellationToken.None);
			var (min, max) = await _databaseService.GetAreaBoundsAsync(obj, city, microDistrict, address, rooms, state, CancellationToken.None);
			var (lotMin, lotMax) = await _databaseService.GetAreaLotBoundsAsync(obj, city, microDistrict, address, rooms, state, CancellationToken.None);
			_statsPriceFrom.Watermark = (priceMin.HasValue ? FormatNumber(priceMin.Value) : null);
			_statsPriceTo.Watermark = (priceMax.HasValue ? FormatNumber(priceMax.Value) : null);
			_statsPricePerSqFrom.Watermark = (pricePerMin.HasValue ? FormatNumber(pricePerMin.Value) : null);
			_statsPricePerSqTo.Watermark = (pricePerMax.HasValue ? FormatNumber(pricePerMax.Value) : null);
			_statsAreaFrom.Watermark = (min.HasValue ? FormatNumber(min.Value) : null);
			_statsAreaTo.Watermark = (max.HasValue ? FormatNumber(max.Value) : null);
			_statsAreaLotFrom.Watermark = (lotMin.HasValue ? FormatNumber(lotMin.Value) : null);
			_statsAreaLotTo.Watermark = (lotMax.HasValue ? FormatNumber(lotMax.Value) : null);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko atnaujinti filtr\ufffd rib\ufffd.", ex3);
			_statsPriceFrom.Watermark = null;
			_statsPriceTo.Watermark = null;
			_statsPricePerSqFrom.Watermark = null;
			_statsPricePerSqTo.Watermark = null;
			_statsAreaFrom.Watermark = null;
			_statsAreaTo.Watermark = null;
			_statsAreaLotFrom.Watermark = null;
			_statsAreaLotTo.Watermark = null;
		}
	}

	private static string FormatNumber(double value)
	{
		return value.ToString("N0", CultureInfo.InvariantCulture);
	}

	private void UpdateStatsSummary(IReadOnlyList<StatsListing> listings, double? aggregatedAvgPricePerSquare, double? aggregatedMinPrice, double? aggregatedMaxPrice, double? aggregatedAvgPrice, string? aggregatedMaxPriceUrl, string? aggregatedMinPriceUrl)
	{
		if (listings.Count == 0)
		{
			_statsSummaryPanel.IsVisible = false;
			_summaryShowPerSqTrend.IsVisible = false;
			_summaryShowPerSqTrend.IsEnabled = false;
			_summaryShowTrend.IsVisible = false;
			return;
		}
		List<(double, StatsListing)> list = new List<(double, StatsListing)>();
		List<double> list2 = new List<double>();
		foreach (StatsListing listing in listings)
		{
			double? num = ParseNullableDouble(listing.Price);
			if (num.HasValue)
			{
				list.Add((num.Value, listing));
			}
			double? num2 = ParseNullableDouble(listing.PricePerSquare);
			if (num2.HasValue)
			{
				list2.Add(num2.Value);
			}
		}
		if (list.Count == 0 && list2.Count == 0)
		{
			_statsSummaryPanel.IsVisible = false;
			_summaryShowPerSqTrend.IsVisible = false;
			_summaryShowPerSqTrend.IsEnabled = false;
			_summaryShowTrend.IsVisible = false;
			return;
		}
		_summaryMaxUrl = (string.IsNullOrWhiteSpace(aggregatedMaxPriceUrl) ? null : aggregatedMaxPriceUrl);
		_summaryMinUrl = (string.IsNullOrWhiteSpace(aggregatedMinPriceUrl) ? null : aggregatedMinPriceUrl);
		(double, StatsListing)? tuple = null;
		(double, StatsListing)? tuple2 = null;
		if (list.Count > 0)
		{
			tuple = list.OrderByDescending<(double, StatsListing), double>(((double Value, StatsListing Listing) p) => p.Value).First();
			tuple2 = list.OrderBy<(double, StatsListing), double>(((double Value, StatsListing Listing) p) => p.Value).First();
		}
		double? num3 = aggregatedMaxPrice ?? tuple?.Item1;
		double? num4 = aggregatedMinPrice ?? tuple2?.Item1;
		double? num5 = aggregatedAvgPrice ?? ((list.Count > 0) ? new double?(list.Average<(double, StatsListing)>(((double Value, StatsListing Listing) p) => p.Value)) : ((double?)null));
		_summaryMaxPrice.Content = (num3.HasValue ? ("Maksimali kaina: " + FormatCurrency(num3.Value)) : "Maksimali kaina: -");
		_summaryMinPrice.Content = (num4.HasValue ? ("Minimali kaina: " + FormatCurrency(num4.Value)) : "Minimali kaina: -");
		_summaryAvgPrice.Text = (num5.HasValue ? ("Vidutinę kainą: " + FormatCurrency(num5.Value)) : "Vidutinę kainą: -");
		if (string.IsNullOrWhiteSpace(_summaryMaxUrl) && tuple.HasValue && num3.HasValue && Math.Abs(tuple.Value.Item1 - num3.Value) < 0.0001)
		{
			_summaryMaxUrl = (string.IsNullOrWhiteSpace(tuple.Value.Item2.AdvertisementUrl) ? null : tuple.Value.Item2.AdvertisementUrl);
		}
		if (string.IsNullOrWhiteSpace(_summaryMinUrl) && tuple2.HasValue && num4.HasValue && Math.Abs(tuple2.Value.Item1 - num4.Value) < 0.0001)
		{
			_summaryMinUrl = (string.IsNullOrWhiteSpace(tuple2.Value.Item2.AdvertisementUrl) ? null : tuple2.Value.Item2.AdvertisementUrl);
		}
		_summaryMaxPrice.Tag = _summaryMaxUrl;
		_summaryMaxPrice.IsEnabled = !string.IsNullOrWhiteSpace(_summaryMaxUrl);
		_summaryMinPrice.Tag = _summaryMinUrl;
		_summaryMinPrice.IsEnabled = !string.IsNullOrWhiteSpace(_summaryMinUrl);
		double? num6 = aggregatedAvgPricePerSquare ?? ((list2.Count > 0) ? new double?(list2.Average()) : ((double?)null));
		_summaryAvgPricePerSq.Text = (num6.HasValue ? ("Vid. €/m²: " + FormatCurrency(num6.Value, perSq: true)) : "Vid. €/m²: -");
		_statsSummaryPanel.IsVisible = true;
		_summaryShowPerSqTrend.IsVisible = list2.Count > 0;
		_summaryShowPerSqTrend.IsEnabled = list2.Count > 0;
		_summaryShowTrend.IsVisible = true;
		_summaryShowTrend.IsEnabled = listings.Count > 0;
	}

	private static string FormatCurrency(double value, bool perSq = false)
	{
		string text = value.ToString("N0", CultureInfo.InvariantCulture);
		return perSq ? (text + " €/m²") : (text + " €");
	}

	private void OnOpenListingClick(object? sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: string tag }) || string.IsNullOrWhiteSpace(tag))
		{
			return;
		}
		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = tag,
				UseShellExecute = true
			};
			Process.Start(startInfo);
		}
		catch (Exception exception)
		{
			_logService.Error("Nepavyko atidaryti nuorodos.", exception);
		}
	}

	private List<(string ExternalId, string SearchObject)> GetHistoryKeys()
	{
		List<(string, string)> list = new List<(string, string)>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (StatsListing lastQueriedStat in _lastQueriedStats)
		{
			if (!string.IsNullOrWhiteSpace(lastQueriedStat.ExternalId))
			{
				string text = lastQueriedStat.ExternalId.Trim();
				string text2 = (lastQueriedStat.SearchObject ?? string.Empty).Trim();
				string item = text + "|" + text2;
				if (hashSet.Add(item))
				{
					list.Add((text, text2));
				}
			}
		}
		return list;
	}

	private async void OnShowPricePerSqTrendClick(object? sender, RoutedEventArgs e)
	{
		try
		{
			if (_lastQueriedStats.Count == 0)
			{
				return;
			}
			List<(string ExternalId, string SearchObject)> listingKeys = GetHistoryKeys();
			if (listingKeys.Count == 0)
			{
				return;
			}
			Dictionary<DateTime, (double Sum, int Count)> aggregates = new Dictionary<DateTime, (double, int)>();
			foreach (var (externalId, searchObject) in listingKeys)
			{
				foreach (StatsListing item in await _databaseService.GetListingHistoryAsync(externalId, searchObject, CancellationToken.None))
				{
					if (!DateTime.TryParse(item.CollectedOn, out var date))
					{
						continue;
					}
					double? pricePerSq = ParseNullableDouble(item.PricePerSquare);
					if (pricePerSq.HasValue)
					{
						DateTime key = date.Date;
						if (aggregates.TryGetValue(key, out var agg))
						{
							aggregates[key] = (agg.Sum + pricePerSq.Value, agg.Count + 1);
						}
						else
						{
							aggregates[key] = (pricePerSq.Value, 1);
						}
					}
				}
			}
			List<(DateTime Key, double)> points = (from p in aggregates.Select(delegate(KeyValuePair<DateTime, (double Sum, int Count)> kv)
				{
					KeyValuePair<DateTime, (double, int)> keyValuePair = kv;
					DateTime key2 = keyValuePair.Key;
					keyValuePair = kv;
					double item2 = keyValuePair.Value.Item1;
					keyValuePair = kv;
					return (Key: key2, item2 / (double)keyValuePair.Value.Item2);
				})
				orderby p.Key
				select p).ToList();
			if (points.Count != 0)
			{
				await ShowLineChartAsync(this, new[]
				{
					new LineSeries("Vidutinis €/m² pokytis", points, Color.Parse("#059669"))
				}, "Vidutinis €/m² pokytis");
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko parodyti €/m² pokycio grafiko.", ex3);
		}
	}

	private async void OnShowTrendClick(object? sender, RoutedEventArgs e)
	{
		try
		{
			if (_lastQueriedStats.Count == 0)
			{
				return;
			}
			List<(string ExternalId, string SearchObject)> listingKeys = GetHistoryKeys();
			if (listingKeys.Count == 0)
			{
				return;
			}
			Dictionary<DateTime, (double Sum, int Count)> aggregates = new Dictionary<DateTime, (double, int)>();
			foreach (var (externalId, searchObject) in listingKeys)
			{
				foreach (StatsListing item in await _databaseService.GetListingHistoryAsync(externalId, searchObject, CancellationToken.None))
				{
					if (!DateTime.TryParse(item.CollectedOn, out var date))
					{
						continue;
					}
					double? price = ParseNullableDouble(item.Price);
					if (price.HasValue)
					{
						DateTime key = date.Date;
						if (aggregates.TryGetValue(key, out var agg))
						{
							aggregates[key] = (agg.Sum + price.Value, agg.Count + 1);
						}
						else
						{
							aggregates[key] = (price.Value, 1);
						}
					}
				}
			}
			List<(DateTime Key, double)> points = (from p in aggregates.Select(delegate(KeyValuePair<DateTime, (double Sum, int Count)> kv)
				{
					KeyValuePair<DateTime, (double, int)> keyValuePair = kv;
					DateTime key2 = keyValuePair.Key;
					keyValuePair = kv;
					double item2 = keyValuePair.Value.Item1;
					keyValuePair = kv;
					return (Key: key2, item2 / (double)keyValuePair.Value.Item2);
				})
				orderby p.Key
				select p).ToList();
			if (points.Count != 0)
			{
				await ShowLineChartAsync(this, new[]
				{
					new LineSeries("Vidutinė kaina pagal filtrus", points, Color.Parse("#1d4ed8"))
				}, "Vidutinė kaina pagal filtrus");
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko parodyti vidutinio kainos pokycio grafiko.", ex3);
		}
	}

	private async void OnHistoryClick(object? sender, RoutedEventArgs e)
	{
		StatsEntry entry = null;
		int num;
		if (sender is Button button)
		{
			object tag = button.Tag;
			entry = tag as StatsEntry;
			num = ((entry != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		if (num == 0)
		{
			return;
		}
		string externalId = entry.ExternalId;
		if (string.IsNullOrWhiteSpace(externalId))
		{
			return;
		}
		try
		{
			IReadOnlyList<StatsListing> history = await _databaseService.GetListingHistoryAsync(externalId, entry.SearchObject, CancellationToken.None);
			if (history.Count == 0)
			{
				return;
			}
			StackPanel listPanel = new StackPanel
			{
				Spacing = 6.0
			};
			StatsListing baseEntry = history.LastOrDefault();
			double? basePriceValue = ((baseEntry == null) ? ((double?)null) : ParseNullableDouble(baseEntry.Price));
			foreach (StatsListing item in history)
			{
				double? price = ParseNullableDouble(item.Price);
				string priceChange = "-";
				if (basePriceValue.HasValue && price.HasValue && basePriceValue.Value > 0.0)
				{
					double delta = (price.Value - basePriceValue.Value) / basePriceValue.Value * 100.0;
					priceChange = FormatPercent(Math.Round(delta, 1));
				}
				TextBlock header = new TextBlock
				{
					Text = $"{NormalizeDate(item.CollectedOn)} | {F(item.Price)} | {F(item.PricePerSquare)} | {priceChange}",
					FontWeight = FontWeight.DemiBold,
					Foreground = Brushes.Black,
					TextWrapping = TextWrapping.Wrap
				};
				TextBlock details = new TextBlock
				{
					Text = $"{F(item.SearchObject)} | {F(item.SearchCity)} | {F(item.Address)} | {F(item.AreaSquare)} m² | {F(item.AreaLot)} a | {F(item.HouseState)}",
					Foreground = Brushes.DimGray,
					FontSize = 14.0,
					TextWrapping = TextWrapping.Wrap
				};
				Grid contentGrid = new Grid
				{
					ColumnDefinitions = new ColumnDefinitions("*,Auto"),
					RowDefinitions = new RowDefinitions("Auto,Auto")
				};
				Grid.SetColumn(header, 0);
				Grid.SetRow(header, 0);
				Grid.SetColumn(details, 0);
				Grid.SetRow(details, 1);
				contentGrid.Children.Add(header);
				contentGrid.Children.Add(details);
				listPanel.Children.Add(new Border
				{
					Padding = new Thickness(8.0),
					Background = new SolidColorBrush(Color.Parse("#f8fafc")),
					BorderBrush = new SolidColorBrush(Color.Parse("#e2e8f0")),
					BorderThickness = new Thickness(1.0),
					CornerRadius = new CornerRadius(8.0),
					Child = contentGrid
				});
			}
			string latestUrl = history.FirstOrDefault()?.AdvertisementUrl;
			Grid root = new Grid
			{
				RowDefinitions = new RowDefinitions("*,Auto")
			};
			ScrollViewer scrollViewer = new ScrollViewer
			{
				Padding = new Thickness(16.0),
				Width = 560.0,
				Height = 360.0,
				Content = listPanel
			};
			Grid.SetRow(scrollViewer, 0);
			root.Children.Add(scrollViewer);
			StackPanel footerPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(16.0, 8.0, 16.0, 12.0),
				Spacing = 8.0
			};
			Button openChartButton = new Button
			{
				Content = "Grafikas",
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(16.0, 8.0),
				Background = new SolidColorBrush(Color.Parse("#1d4ed8")),
				BorderBrush = new SolidColorBrush(Color.Parse("#1d4ed8")),
				BorderThickness = new Thickness(0.0),
				Foreground = Brushes.White,
				FontWeight = FontWeight.DemiBold,
				CornerRadius = new CornerRadius(999.0),
				IsEnabled = (history.Count > 1 && history.Any((StatsListing h) => ParseNullableDouble(h.Price).HasValue))
			};
			openChartButton.Click += delegate
			{
				ShowPriceChart(history);
			};
			Button openLatestButton = new Button
			{
				Content = "Nuoroda",
				Tag = latestUrl,
				IsEnabled = !string.IsNullOrWhiteSpace(latestUrl),
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(16.0, 8.0),
				Background = new SolidColorBrush(Color.Parse("#1d4ed8")),
				BorderBrush = new SolidColorBrush(Color.Parse("#1d4ed8")),
				BorderThickness = new Thickness(0.0),
				Foreground = Brushes.White,
				FontWeight = FontWeight.DemiBold,
				CornerRadius = new CornerRadius(999.0)
			};
			openLatestButton.Click += OnOpenListingClick;
			footerPanel.Children.Add(openChartButton);
			footerPanel.Children.Add(openLatestButton);
			Grid.SetRow(footerPanel, 1);
			root.Children.Add(footerPanel);
			Window dialog = new Window
			{
				Title = "Istorija",
				SizeToContent = SizeToContent.WidthAndHeight,
				CanResize = false,
				Content = root
			};
			WindowBase owner = base.Owner;
			dialog.WindowStartupLocation = ((!(owner is Window)) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner);
			await dialog.ShowDialog(this);
		}
		catch (Exception exception)
		{
			_logService.Error("Nepavyko parodyti istorijos.", exception);
		}
		static string F(string? value)
		{
			return string.IsNullOrWhiteSpace(value) ? "-" : value;
		}
		static string FormatPercent(double value)
		{
			return $"{value:+0.0;-0.0;0}%";
		}
		static string NormalizeDate(string collectedOn)
		{
			DateTime result;
			return DateTime.TryParse(collectedOn, out result) ? result.ToString("yyyy-MM-dd") : collectedOn;
		}
	}

	private async void ShowPriceChart(IReadOnlyList<StatsListing> history)
	{
		try
		{
			List<(DateTime date, double Value)> pointsData = (from p in history.Select(delegate(StatsListing item, int index)
				{
					double? item2 = ParseNullableDouble(item.Price);
					DateTime result;
					DateTime item3 = (DateTime.TryParse(item.CollectedOn, out result) ? result : DateTime.UtcNow.AddDays(-history.Count + index));
					return (date: item3, price: item2);
				}).Where(delegate((DateTime date, double? price) p)
				{
					(DateTime, double?) tuple = p;
					return tuple.Item2.HasValue;
				})
				orderby p.date
				select p).Select(delegate((DateTime date, double? price) p)
			{
			DateTime item = p.date;
			(DateTime, double?) tuple = p;
			return (date: item, Value: tuple.Item2.Value);
		}).ToList();
			if (pointsData.Count >= 2)
			{
				await MainWindow.ShowLineChartAsync(this, new[]
				{
					new LineSeries("Kainos pokytis", pointsData, Color.Parse("#1d4ed8"))
				}, "Kainos pokytis");
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			_logService.Error("Nepavyko parodyti kainos grafiko.", ex3);
		}
	}

		private sealed class LineSeries
		{
			public LineSeries(string name, IReadOnlyList<(DateTime date, double value)> points, Color? explicitColor = null)
			{
				Name = name;
				Points = points;
				ExplicitColor = explicitColor;
			}

			public string Name { get; }

			public IReadOnlyList<(DateTime date, double value)> Points { get; }

			public Color? ExplicitColor { get; }
		}

		private sealed class LineVisualState
		{
			public LineVisualState(Polyline line)
			{
				Line = line;
			}

			public Polyline Line { get; }

			public List<Ellipse> Markers { get; } = new List<Ellipse>();

			public Border? LegendSwatch { get; set; }
		}

		private static Task ShowLineChartAsync(Window owner, IReadOnlyList<LineSeries> series, string title, string? resultsText = null)
		{
			if (owner == null || series == null)
			{
				return Task.CompletedTask;
			}

			List<LineSeries> validSeries = series.Where((LineSeries s) => s != null && s.Points.Count > 0).ToList();
			if (validSeries.Count == 0)
			{
				return Task.CompletedTask;
			}

			List<double> values = validSeries.SelectMany((LineSeries s) => s.Points.Select((point) => point.value)).ToList();
			if (values.Count == 0)
			{
				return Task.CompletedTask;
			}

			double minValue = values.Min();
			double maxValue = values.Max();
			if (Math.Abs(maxValue - minValue) < 0.0001)
			{
				maxValue = minValue + 1.0;
			}

			List<DateTime> allDates = validSeries.SelectMany((LineSeries s) => s.Points.Select((point) => point.date)).Distinct().OrderBy((DateTime d) => d).ToList();
			if (allDates.Count == 0)
			{
				return Task.CompletedTask;
			}

			Dictionary<DateTime, int> dateIndex = new Dictionary<DateTime, int>();
			for (int i = 0; i < allDates.Count; i++)
			{
				dateIndex[allDates[i]] = i;
			}

			double plotWidth = 632.0;
			double plotHeight = 316.0;
			Canvas canvas = new Canvas
			{
				Width = 720.0,
				Height = 420.0,
				Background = Brushes.White
			};
			Window? chartWindow = null;

			for (int i2 = 0; i2 <= 6; i2++)
			{
				double ratio = (double)i2 / 6.0;
				double y = 40.0 + (1.0 - ratio) * plotHeight;
				Line gridLine = new Line
				{
					StartPoint = new Point(64.0, y),
					EndPoint = new Point(64.0 + plotWidth, y),
					Stroke = new SolidColorBrush(Color.Parse("#e2e8f0")),
					StrokeThickness = 1.0
				};
				canvas.Children.Add(gridLine);
				double value = minValue + (maxValue - minValue) * ratio;
				TextBlock label = new TextBlock
				{
					Text = $"{value:0}",
					Foreground = Brushes.Gray,
					FontSize = 12.0
				};
				Canvas.SetLeft(label, 8.0);
				Canvas.SetTop(label, y - 10.0);
				canvas.Children.Add(label);
			}

			Line yAxis = new Line
			{
				StartPoint = new Point(64.0, 40.0),
				EndPoint = new Point(64.0, 40.0 + plotHeight),
				Stroke = new SolidColorBrush(Color.Parse("#cbd5e1")),
				StrokeThickness = 1.5
			};
			canvas.Children.Add(yAxis);
			Line xAxis = new Line
			{
				StartPoint = new Point(64.0, 40.0 + plotHeight),
				EndPoint = new Point(64.0 + plotWidth, 40.0 + plotHeight),
				Stroke = new SolidColorBrush(Color.Parse("#cbd5e1")),
				StrokeThickness = 1.5
			};
			canvas.Children.Add(xAxis);

			int xLabelCount = Math.Min(allDates.Count, 6);
			for (int i3 = 0; i3 < xLabelCount; i3++)
			{
				int idx = ((xLabelCount != 1) ? ((int)Math.Round((double)(i3 * (allDates.Count - 1)) / (double)(xLabelCount - 1))) : 0);
				double ratioX = ((allDates.Count == 1) ? 0.0 : ((double)idx / (double)(allDates.Count - 1)));
				double x = 64.0 + ratioX * plotWidth;
				Line tick = new Line
				{
					StartPoint = new Point(x, 40.0 + plotHeight),
					EndPoint = new Point(x, 40.0 + plotHeight + 6.0),
					Stroke = new SolidColorBrush(Color.Parse("#cbd5e1")),
					StrokeThickness = 1.0
				};
				canvas.Children.Add(tick);
				TextBlock label2 = new TextBlock
				{
					Text = allDates[idx].ToString("yyyy-MM-dd"),
					Foreground = Brushes.Gray,
					FontSize = 12.0
				};
				Canvas.SetLeft(label2, x - 36.0);
				Canvas.SetTop(label2, 40.0 + plotHeight + 8.0);
				canvas.Children.Add(label2);
			}

			Color[] palette = new Color[]
			{
				Color.Parse("#1d4ed8"),
				Color.Parse("#2563eb"),
				Color.Parse("#9333ea"),
				Color.Parse("#f97316"),
				Color.Parse("#059669"),
				Color.Parse("#db2777"),
				Color.Parse("#0ea5e9"),
				Color.Parse("#10b981")
			};

			Dictionary<LineSeries, Color> assignedColors = new Dictionary<LineSeries, Color>();
			Dictionary<LineSeries, LineVisualState> lineVisualStates = new Dictionary<LineSeries, LineVisualState>();
			int paletteIndex = 0;
			foreach (LineSeries lineSeries in validSeries)
			{
				Color color2 = lineSeries.ExplicitColor ?? palette[paletteIndex % palette.Length];
				assignedColors[lineSeries] = color2;
				paletteIndex++;
			}

			void UpdateSeriesColor(LineVisualState visual, Color color)
			{
				visual.Line.Stroke = new SolidColorBrush(color);
				foreach (Ellipse marker in visual.Markers)
				{
					marker.Fill = new SolidColorBrush(color);
				}

				if (visual.LegendSwatch != null)
				{
					visual.LegendSwatch.Background = new SolidColorBrush(color);
				}
			}

			foreach (LineSeries lineSeries2 in validSeries)
			{
				Color color3 = assignedColors[lineSeries2];
				Polyline polyline2 = new Polyline
				{
					Stroke = new SolidColorBrush(color3),
					StrokeThickness = 2.0
				};
				LineVisualState lineState = new LineVisualState(polyline2);
				foreach ((DateTime date, double value2) in lineSeries2.Points)
				{
					if (!dateIndex.TryGetValue(date, out var indexValue))
					{
						continue;
					}
					double ratioX2 = ((allDates.Count == 1) ? 0.0 : ((double)indexValue / (double)(allDates.Count - 1)));
					double x2 = 64.0 + ratioX2 * plotWidth;
					double ratioY2 = (value2 - minValue) / (maxValue - minValue);
					double y2 = 40.0 + (1.0 - ratioY2) * plotHeight;
					polyline2.Points.Add(new Point(x2, y2));
					Ellipse marker2 = new Ellipse
					{
						Width = 8.0,
						Height = 8.0,
						Fill = new SolidColorBrush(color3)
					};
					lineState.Markers.Add(marker2);
					Canvas.SetLeft(marker2, x2 - 4.0);
					Canvas.SetTop(marker2, y2 - 4.0);
					canvas.Children.Add(marker2);
					ToolTip.SetTip(marker2, $"{lineSeries2.Name}: {value2:0} € ({date:yyyy-MM-dd})");
					ToolTip.SetTip(marker2, $"{lineSeries2.Name}: {value2:0} € ({date:yyyy-MM-dd})");
				}
				canvas.Children.Add(polyline2);
				lineVisualStates[lineSeries2] = lineState;
			}

			StackPanel legendRoot = new StackPanel
			{
				Orientation = Orientation.Vertical,
				Spacing = 4.0,
				Margin = new Thickness(0.0, 8.0, 0.0, 8.0)
			};
			foreach (LineSeries lineSeries3 in validSeries)
			{
				Color color4 = assignedColors[lineSeries3];
				StackPanel legendRow = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Spacing = 6.0
				};
				LineVisualState lineState = lineVisualStates[lineSeries3];
				Border colorSwatch = new Border
				{
					Width = 16.0,
					Height = 10.0,
					CornerRadius = new CornerRadius(4.0),
					Background = new SolidColorBrush(color4),
					Cursor = new Cursor(StandardCursorType.Hand)
				};
				lineState.LegendSwatch = colorSwatch;
				ColorPicker popupPicker = new ColorPicker
				{
					Color = color4,
					Width = 320.0,
					Height = 220.0
				};
				Border popupHost = new Border
				{
					Padding = new Thickness(12),
					Background = Brushes.Black,
					Child = popupPicker
				};
				Popup popup = new Popup
				{
					PlacementTarget = colorSwatch,
					Placement = PlacementMode.Bottom,
					Child = popupHost
				};
				bool pointerOverSwatch = false;
				bool pointerOverPopup = false;
				DispatcherTimer? closeTimer = null;
				void ScheduleClose()
				{
					closeTimer?.Stop();
					closeTimer = new DispatcherTimer
					{
						Interval = TimeSpan.FromMilliseconds(200)
					};
					closeTimer.Tick += (_, _) =>
					{
						closeTimer?.Stop();
						if (!pointerOverSwatch && !pointerOverPopup)
						{
							popup.IsOpen = false;
						}
					};
					closeTimer.Start();
				}
				void RefreshPopupColor(Color color)
				{
					popupPicker.Color = color;
					colorSwatch.Background = new SolidColorBrush(color);
					assignedColors[lineSeries3] = color;
					UpdateSeriesColor(lineState, color);
				}
				popupPicker.PropertyChanged += (_, e) =>
				{
					if (e.Property == ColorPicker.ColorProperty)
					{
						RefreshPopupColor(popupPicker.Color);
					}
				};
				colorSwatch.PointerEntered += (_, _) =>
				{
					pointerOverSwatch = true;
					RefreshPopupColor(assignedColors[lineSeries3]);
					popup.IsOpen = true;
				};
				colorSwatch.PointerExited += (_, _) =>
				{
					pointerOverSwatch = false;
					ScheduleClose();
				};
				popupHost.PointerEntered += (_, _) =>
				{
					pointerOverPopup = true;
				};
				popupHost.PointerExited += (_, _) =>
				{
					pointerOverPopup = false;
					ScheduleClose();
				};
				legendRow.Children.Add(colorSwatch);
				legendRow.Children.Add(new TextBlock
				{
					Text = lineSeries3.Name,
					Foreground = Brushes.DimGray,
					FontSize = 12.0,
					VerticalAlignment = VerticalAlignment.Center
				});
				legendRoot.Children.Add(legendRow);
			}

			TextBlock titleBlock = new TextBlock
			{
				Text = title,
				HorizontalAlignment = HorizontalAlignment.Center,
				FontSize = 16.0,
				FontWeight = FontWeight.DemiBold,
				Foreground = Brushes.DimGray,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			};

			Grid layout = new Grid();
			layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			if (!string.IsNullOrWhiteSpace(resultsText))
			{
				layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			}
			layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
			layout.Children.Add(titleBlock);
			Grid.SetRow(legendRoot, 1);
			layout.Children.Add(legendRoot);
			int canvasRow = layout.RowDefinitions.Count - 1;
			if (!string.IsNullOrWhiteSpace(resultsText))
			{
				TextBlock resultsBlock = new TextBlock
				{
					Text = resultsText,
					HorizontalAlignment = HorizontalAlignment.Right,
					Foreground = Brushes.Gray,
					FontSize = 12.0,
					Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
				};
				Grid.SetRow(resultsBlock, 2);
				layout.Children.Add(resultsBlock);
			}
			Grid.SetRow(canvas, canvasRow);
			layout.Children.Add(canvas);

			chartWindow = new Window
			{
				Title = title,
				SizeToContent = SizeToContent.WidthAndHeight,
				CanResize = false,
				Content = new Border
				{
					Background = Brushes.White,
					Padding = new Thickness(12.0),
					Child = layout
				}
			};
			chartWindow.WindowStartupLocation = ((!(owner is Window)) ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner);
			chartWindow.Show(owner);
			return Task.CompletedTask;
		}

		private static Task<Color?> ShowColorPickerDialogAsync(Window owner, Color initialColor)
		{
			TaskCompletionSource<Color?> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
			Color currentColor = initialColor;
			Border colorPreview = new Border
			{
				CornerRadius = new CornerRadius(4.0),
				Height = 32.0,
				Background = new SolidColorBrush(initialColor),
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
			};

			ColorPicker colorPicker = new ColorPicker
			{
				Color = initialColor,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Width = 360.0,
				Height = 220.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
			};

			TextBox hexBox = new TextBox
			{
				Text = initialColor.ToString(),
				IsReadOnly = true,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Background = Brushes.Transparent,
				Foreground = Brushes.White,
				BorderThickness = new Thickness(0),
				FontSize = 18.0
			};

			Border primaryBar = new Border
			{
				CornerRadius = new CornerRadius(8.0),
				Background = new SolidColorBrush(initialColor),
				Height = 40.0
			};

			Border secondaryBar = new Border
			{
				CornerRadius = new CornerRadius(8.0),
				Background = new SolidColorBrush(AdjustLuminance(initialColor, 0.85)),
				Height = 40.0
			};

			void RefreshColor(Color color)
			{
				currentColor = color;
				colorPreview.Background = new SolidColorBrush(color);
				primaryBar.Background = new SolidColorBrush(color);
				secondaryBar.Background = new SolidColorBrush(AdjustLuminance(color, 0.85));
				hexBox.Text = color.ToString();
			}

			colorPicker.PropertyChanged += (_, e) =>
			{
				if (e.Property == ColorPicker.ColorProperty)
				{
					RefreshColor(colorPicker.Color);
				}
			};

			Button okButton = new Button
			{
				Content = "Pasirinkti",
				IsDefault = true
			};
			Button cancelButton = new Button
			{
				Content = "Atšaukti",
				IsCancel = true
			};

			StackPanel buttonPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 8.0,
				HorizontalAlignment = HorizontalAlignment.Right,
				Children = { okButton, cancelButton }
			};

			Grid pickerGrid = new Grid
			{
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			};
			pickerGrid.Children.Add(colorPicker);

			StackPanel content = new StackPanel
			{
				Spacing = 8.0,
				Margin = new Thickness(16.0),
				Children =
				{
					new Border
					{
						CornerRadius = new CornerRadius(8.0),
						Background = Brushes.DodgerBlue,
						Height = 40.0
					},
					secondaryBar,
					primaryBar,
					colorPreview,
					pickerGrid,
					new Border
					{
						CornerRadius = new CornerRadius(6.0),
						BorderBrush = Brushes.Teal,
						BorderThickness = new Thickness(2.0),
						Padding = new Thickness(8.0),
						Background = Brushes.Black,
						Child = hexBox
					},
					new Border
					{
						Background = Brushes.Transparent,
						Child = buttonPanel
					}
				}
			};

			Window dialog = new Window
			{
				Title = "Pasirinkite spalvą",
				SizeToContent = SizeToContent.Manual,
				MinWidth = 420.0,
				MinHeight = 360.0,
				CanResize = false,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Content = new Border
				{
					Background = Brushes.White,
					Child = content
				}
			};

			okButton.Click += (_, _) =>
			{
				hexBox.Text = currentColor.ToString();
				tcs.TrySetResult(currentColor);
				dialog.Close();
			};

			cancelButton.Click += (_, _) =>
			{
				tcs.TrySetResult(null);
				dialog.Close();
			};

			dialog.Closed += (_, _) =>
			{
				if (!tcs.Task.IsCompleted)
				{
					tcs.TrySetResult(null);
				}
			};

			dialog.Show(owner);
			return tcs.Task;
		}

		private static Color AdjustLuminance(Color color, double factor)
		{
			factor = Math.Clamp(factor, 0.0, 1.0);
			byte Adjust(byte channel)
			{
				return (byte)Math.Round(channel * factor + 255.0 * (1.0 - factor));
			}

			return Color.FromRgb(Adjust(color.R), Adjust(color.G), Adjust(color.B));
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
