using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Maui.Controls;

namespace lab1
{
    public partial class MainPage : ContentPage
    {
        private int rowCount = 3;  
        private int columnCount = 3;  
        private bool isExpressionMode = true;  

        // Зберігає початкові вирази
        private Dictionary<(int row, int col), string> cellExpressions = new Dictionary<(int, int), string>();

        public MainPage()
        {
            InitializeComponent();
            InitializeTable();
        }

        // Ініціалізація таблиці з базовою кількістю рядків і стовпців
        private void InitializeTable()
        {
            for (int row = 0; row < rowCount; row++)
            {
                DataTable.RowDefinitions.Add(new RowDefinition());
                for (int col = 0; col < columnCount; col++)
                {
                    if (row == 0)
                        DataTable.ColumnDefinitions.Add(new ColumnDefinition());

                    var cell = new Entry { Placeholder = $"R{row + 1}C{col + 1}" };
                    cell.TextChanged += OnCellTextChanged;
                    DataTable.Add(cell, col, row);
                    cellExpressions[(row, col)] = string.Empty; 
                }
            }
        }

        // Збереження змін у виразі комірки
        private void OnCellTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is Entry entry)
            {
                int row = Grid.GetRow(entry);
                int col = Grid.GetColumn(entry);
                cellExpressions[(row, col)] = entry.Text; 
            }
        }
        
        private void OnAddRowClicked(object sender, EventArgs e)
        {
            DataTable.RowDefinitions.Add(new RowDefinition());
            for (int col = 0; col < columnCount; col++)
            {
                var cell = new Entry { Placeholder = $"R{rowCount + 1}C{col + 1}" };
                cell.TextChanged += OnCellTextChanged;
                DataTable.Add(cell, col, rowCount);
                cellExpressions[(rowCount, col)] = string.Empty;
            }
            rowCount++;
        }

        private void OnAddColumnClicked(object sender, EventArgs e)
        {
            DataTable.ColumnDefinitions.Add(new ColumnDefinition());
            for (int row = 0; row < rowCount; row++)
            {
                var cell = new Entry { Placeholder = $"R{row + 1}C{columnCount + 1}" };
                cell.TextChanged += OnCellTextChanged;
                DataTable.Add(cell, columnCount, row);
                cellExpressions[(row, columnCount)] = string.Empty;
            }
            columnCount++;
        }

        private bool IsExpressionValid(string expression, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!new Regex(@"^[\d\+\-\*/\^\(\)\sA-Za-z0-9,]+$").IsMatch(expression))
            {
                errorMessage = "Вираз містить недопустимі символи.";
                return false;
            }

            Stack<char> stack = new Stack<char>();
            foreach (char c in expression)
            {
                if (c == '(') stack.Push(c);
                else if (c == ')')
                {
                    if (stack.Count == 0) return false;
                    stack.Pop();
                }
            }
            if (stack.Count != 0)
            {
                errorMessage = "Невідповідність дужок у виразі.";
                return false;
            }

            return true;
        }

        private void OnRemoveRowClicked(object sender, EventArgs e)
        {
            if (rowCount > 1) 
            {
                DataTable.RowDefinitions.RemoveAt(rowCount - 1); 
                for (int col = 0; col < columnCount; col++)
                {
                    var cell = DataTable.Children.OfType<Entry>()
                                                  .FirstOrDefault(c => Grid.GetRow(c) == rowCount - 1 && Grid.GetColumn(c) == col);
                    if (cell != null)
                    {
                        DataTable.Children.Remove(cell); 
                    }
                }
                rowCount--;
            }
        }

        private void OnRemoveColumnClicked(object sender, EventArgs e)
        {
            if (columnCount > 1) 
                DataTable.ColumnDefinitions.RemoveAt(columnCount - 1); 
                for (int row = 0; row < rowCount; row++)
                {
                    var cell = DataTable.Children.OfType<Entry>()
                                                  .FirstOrDefault(c => Grid.GetRow(c) == row && Grid.GetColumn(c) == columnCount - 1);
                    if (cell != null)
                    {
                        DataTable.Children.Remove(cell); 
                    }
                }
                columnCount--;
            }
        }

        // Обчислення значення виразу з підтримкою посилань на комірки
        private double EvaluateExpression(string expression, HashSet<(int, int)> visitedCells = null)
        {
            if (visitedCells == null)
            {
                visitedCells = new HashSet<(int, int)>();
            }

            expression = ReplaceCellReferences(expression, visitedCells);

            expression = EvaluatePower(expression);

            expression = EvaluateMinMaxFunctions(expression);

            DataTable dt = new DataTable();
            var result = dt.Compute(expression, "");
            return Convert.ToDouble(result);
        }

        private string EvaluatePower(string expression)
        {
            var powerPattern = new Regex(@"\b(\d+(\.\d+)?|\w+)\s*\^\s*(\d+(\.\d+)?|\w+)\b");

            while (powerPattern.IsMatch(expression))
            {
                expression = powerPattern.Replace(expression, match =>
                {
                    double baseValue = Convert.ToDouble(EvaluateExpression(match.Groups[1].Value));
                    double exponent = Convert.ToDouble(EvaluateExpression(match.Groups[3].Value));
                    double powerResult = Math.Pow(baseValue, exponent);
                    return powerResult.ToString();
                });
            }

            return expression;
        }

        private string EvaluateMinMaxFunctions(string expression)
        {
            var minPattern = new Regex(@"min\s*\(\s*([^\)]+)\s*\)");
            expression = minPattern.Replace(expression, match =>
            {
                string[] numbers = match.Groups[1].Value.Split(',').Select(n => n.Trim()).ToArray();
                double minValue = numbers.Select(n => Convert.ToDouble(EvaluateExpression(n))).Min();
                return minValue.ToString();
            });

            var maxPattern = new Regex(@"max\s*\(\s*([^\)]+)\s*\)");
            expression = maxPattern.Replace(expression, match =>
            {
                string[] numbers = match.Groups[1].Value.Split(',').Select(n => n.Trim()).ToArray();
                double maxValue = numbers.Select(n => Convert.ToDouble(EvaluateExpression(n))).Max();
                return maxValue.ToString();
            });

            return expression;
        }

        private string ReplaceCellReferences(string expression, HashSet<(int, int)> visitedCells)
        {
            // Зразок для розпізнавання посилань на комірки, наприклад, A1, B2
            var cellRefPattern = new Regex(@"([A-Z]+)(\d+)");
            return cellRefPattern.Replace(expression, match =>
            {
                string columnName = match.Groups[1].Value; 
                int rowNumber = int.Parse(match.Groups[2].Value) - 1;

                int colIndex = 0;
                foreach (char c in columnName)
                {
                    colIndex = colIndex * 26 + (c - 'A' + 1);
                }
                colIndex--; 

                // Перевірка на рекурсію
                if (visitedCells.Contains((rowNumber, colIndex)))
                {
                    throw new InvalidOperationException("Виявлено рекурсивне посилання!");
                }

                visitedCells.Add((rowNumber, colIndex)); 

                if (cellExpressions.TryGetValue((rowNumber, colIndex), out string cellExpression))
                {
                    if (IsExpressionValid(cellExpression, out _))
                    {
                        double result = EvaluateExpression(cellExpression, new HashSet<(int, int)>(visitedCells));
                        return result.ToString();
                    }
                }
                return "0"; 
            });
        }


        // Обробка перемикання режиму "Вираз/Значення"
        private void OnModeSwitchToggled(object sender, ToggledEventArgs e)
        {
            isExpressionMode = e.Value;
            foreach (var entry in DataTable.Children.OfType<Entry>())
            {
                int row = Grid.GetRow(entry);
                int col = Grid.GetColumn(entry);
                string expression = cellExpressions[(row, col)];

                if (isExpressionMode)
                {
                    entry.Text = expression; 
                }
                else
                {
                    if (IsExpressionValid(expression, out string errorMessage))
                    {
                        double result = EvaluateExpression(expression);
                        entry.Text = result.ToString();
                    }
                    else
                    {
                        entry.Text = "Помилка";
                        ErrorLabel.Text = errorMessage;
                    }
                }
            }
        }
    }
}
