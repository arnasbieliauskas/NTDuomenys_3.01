$path = "MainWindow.axaml.cs"
$content = Get-Content $path -Raw -Encoding utf8
$pattern = '(?s)\t+Button leftPanel = new Button.*?\t+outerGrid\.Children\.Add\(leftPanel\);\r?\n'
$replacement = @"
            StackPanel buttonColumn = new StackPanel
            {
                Spacing = 8.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
            };
            Button addCityButton = new Button
            {
                Content = "Pridėti miestą/gyvenvietę",
                MinWidth = 200.0,
                Padding = new Thickness(18.0, 12.0),
                FontSize = 14.0,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            addCityButton.Classes.Add("primary");
            addCityButton.Click += delegate
            {
                AddCityCombo();
            };
            _removeCityButton = new Button
            {
                Content = "Pašalinti miestą/gyvenvietę",
                MinWidth = 200.0,
                Padding = new Thickness(18.0, 12.0),
                FontSize = 14.0,
                FontWeight = FontWeight.SemiBold,
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
            buttonColumn.Children.Add(addCityButton);
            buttonColumn.Children.Add(_removeCityButton);
            Grid.SetColumn(buttonColumn, 0);
            outerGrid.Children.Add(buttonColumn);
"@
$newContent = [regex]::Replace($content, $pattern, $replacement)
Set-Content $path -Value $newContent -Encoding utf8
