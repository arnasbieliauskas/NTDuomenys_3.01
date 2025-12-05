$path = "MainWindow.axaml.cs"
$content = Get-Content $path -Raw -Encoding utf8
$pattern = '(?s)\s+StackPanel buttonColumn = new StackPanel.*?outerGrid\.Children\.Add\(buttonColumn\);\r?\n'
$replacement = @"
\t\tStackPanel buttonColumn = new StackPanel
\t\t{
\t\t\tSpacing = 8.0,
\t\t\tHorizontalAlignment = HorizontalAlignment.Stretch,
\t\t\tMargin = new Thickness(0.0, 8.0, 0.0, 0.0)
\t\t};
\t\tButton addCityButton = new Button
\t\t{
\t\t\tContent = "Pridėti miestą/gyvenvietę",
\t\t\tMinWidth = 200.0,
\t\t\tPadding = new Thickness(18.0, 12.0),
\t\t\tFontSize = 14.0,
\t\t\tFontWeight = FontWeight.SemiBold,
\t\t\tHorizontalAlignment = HorizontalAlignment.Stretch,
\t\t\tVerticalAlignment = VerticalAlignment.Center,
\t\t\tHorizontalContentAlignment = HorizontalAlignment.Center,
\t\t\tVerticalContentAlignment = VerticalAlignment.Center,
\t\t\tCursor = new Cursor(StandardCursorType.Hand)
\t\t};
\t\taddCityButton.Classes.Add("primary");
\t\taddCityButton.Click += delegate
\t\t{
\t\t\tAddCityCombo();
\t\t};
\t\t_removeCityButton = new Button
\t\t{
\t\t\tContent = "Pašalinti miestą/gyvenvietę",
\t\t\tMinWidth = 200.0,
\t\t\tPadding = new Thickness(18.0, 12.0),
\t\t\tFontSize = 14.0,
\t\t\tFontWeight = FontWeight.SemiBold,
\t\t\tHorizontalAlignment = HorizontalAlignment.Stretch,
\t\t\tVerticalAlignment = VerticalAlignment.Center,
\t\t\tHorizontalContentAlignment = HorizontalAlignment.Center,
\t\t\tVerticalContentAlignment = VerticalAlignment.Center,
\t\t\tCursor = new Cursor(StandardCursorType.Hand)
\t\t};
\t\t_removeCityButton.Classes.Add("primary");
\t\t_removeCityButton.Click += delegate
\t\t{
\t\t\tRemoveCityCombo();
\t\t};
\t\tUpdateRemoveCityButtonState();
\t\tbuttonColumn.Children.Add(addCityButton);
\t\tbuttonColumn.Children.Add(_removeCityButton);
\t\tGrid.SetColumn(buttonColumn, 0);
\t\touterGrid.Children.Add(buttonColumn);
"@
$newContent = [regex]::Replace($content, $pattern, $replacement)
Set-Content $path -Value $newContent -Encoding utf8
