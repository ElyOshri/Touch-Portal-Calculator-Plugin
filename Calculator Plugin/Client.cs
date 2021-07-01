using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Data;
using TouchPortalSDK;
using TouchPortalSDK.Interfaces;
using TouchPortalSDK.Messages.Events;
using TouchPortalSDK.Messages.Models;

namespace Calculator_Plugin
{
    public class Client : ITouchPortalEventHandler
    {
        public string PluginId => "TPCalculator";

        private readonly ITouchPortalClient _client;

        private IReadOnlyCollection<Setting> _settings;


        private DataTable compiler = new DataTable();
        private string currentEquation = "", lastResult = "", beforePercentage = "", afterPercentage = "", percentage = "", actualPower = "", actualNumber = "";
        private int currentPosition = 0, startOfValue = 0, hasPercentage = 0, hasPower = 0, powerEnd = 0, bracketAmount = 0, afterBracketAmount = 0, currentlySelectedMemory = 1, historyAmount = 0, memoryAmount = 0;
        private bool calculated = false;
        private string[,] historyMatrix;
        private string[] memoryArr, historyChoice, memoryChoice;
        
        public Client()
        {
            _client = TouchPortalFactory.CreateClient(this);
        }

        public void Run()
        {
            _client.Connect();
        }

        public void OnClosedEvent(string message)
        {
            Console.WriteLine("TouchPortal Disconnected.");

            //Optional force exits this plugin.
            Environment.Exit(0);
        }

        public void OnInfoEvent(InfoEvent message)
        {
            Console.WriteLine($"[Info] VersionCode: '{message.TpVersionCode}', VersionString: '{message.TpVersionString}', SDK: '{message.SdkVersion}', PluginVersion: '{message.PluginVersion}', Status: '{message.Status}'");
            _settings = message.Settings;
            
            foreach (var setting in _settings)
            {
                if (setting.Name == "History Amount")
                    historyAmount = Convert.ToInt32(setting.Value);
                else if (setting.Name == "Memory Amount")
                    memoryAmount = Convert.ToInt32(setting.Value);
            }

            historyMatrix = new string[2, historyAmount];
            memoryArr = new string[memoryAmount];

            historyChoice = new string[historyAmount];
            memoryChoice = new string[memoryAmount];

            for (int i = 1; i <= historyAmount; i++)
            {
                _client.CreateState("TPPlugin.Calculator.States.History.Equation." + i, "Calculator: History Equation - Number " + i, "");
                _client.CreateState("TPPlugin.Calculator.States.History.Result." + i, "Calculator: History Result - Number " + i, "");
                historyChoice[i - 1] = i.ToString();
            }

            for (int i = 1; i <= memoryAmount; i++)
            {
                _client.CreateState("TPPlugin.Calculator.States.MemoryValue." + i, "Calculator: Memory Value - Number " + i, "");
                memoryChoice[i - 1] = i.ToString();
            }

            _client.ChoiceUpdate("TPPlugin.Calculator.Actions.MemorySelect.Number.Data.List", memoryChoice);
            _client.ChoiceUpdate("TPPlugin.Calculator.Actions.AddFromHistory.Number.Data.List", historyChoice);


        }

        public void OnListChangedEvent(ListChangeEvent message)
        {
            Console.WriteLine($"[OnListChanged] {message.ListId}/{message.ActionId}/{message.InstanceId} '{message.Value}'");
        }

        public void OnBroadcastEvent(BroadcastEvent message)
        {
            Console.WriteLine($"[Broadcast] Event: '{message.Event}', PageName: '{message.PageName}'");
        }

        public void OnSettingsEvent(SettingsEvent message)
        {
            _settings = message.Values;
            for (int i = 1; i <= historyAmount; i++)
            {
                _client.RemoveState("TPPlugin.Calculator.States.History.Result." + i);
                _client.RemoveState("TPPlugin.Calculator.States.History.Equation." + i);
            }
            for (int i = 1; i <= memoryAmount; i++)
            {
                _client.RemoveState("TPPlugin.Calculator.States.MemoryValue." + i);
            }
            foreach (var setting in _settings)
            {
                if (setting.Name == "History Amount")
                    historyAmount = Convert.ToInt32(setting.Value);
                else if (setting.Name == "Memory Amount")
                    memoryAmount = Convert.ToInt32(setting.Value);
            }
            historyMatrix = new string[2, historyAmount];
            memoryArr = new string[memoryAmount];
            
            historyChoice = new string[historyAmount];
            memoryChoice = new string[memoryAmount];
            
            for (int i = 1; i <= historyAmount; i++)
            {
                _client.CreateState("TPPlugin.Calculator.States.History.Equation."+i, "Calculator: History Equation - Number "+i, "");
                _client.CreateState("TPPlugin.Calculator.States.History.Result." + i, "Calculator: History Result - Number " + i, "");
                historyChoice[i - 1] = i.ToString();
            }
            
            for (int i = 1; i <= memoryAmount; i++)
            {
                _client.CreateState("TPPlugin.Calculator.States.MemoryValue." + i, "Calculator: Memory Value - Number " + i, "");
                memoryChoice[i - 1] = i.ToString();
            }

            _client.ChoiceUpdate("TPPlugin.Calculator.Actions.MemorySelect.Number.Data.List", memoryChoice);
            _client.ChoiceUpdate("TPPlugin.Calculator.Actions.AddFromHistory.Number.Data.List", historyChoice);

        }

        public void OnActionEvent(ActionEvent message)
        {
            switch (message.ActionId)
            {
                case "TPPlugin.Calculator.Actions.AddValue":
                    if (calculated)
                    {
                        this.currentEquation = "";
                        this.calculated = false;
                        this.hasPercentage = 0;
                        this.hasPower = 0;
                    }
                    this.currentEquation += message.GetValue("TPPlugin.Calculator.Actions.AddValue.Data.List") ?? "";
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;


                case "TPPlugin.Calculator.Actions.AddOperator":
                    if (calculated)
                    {
                        this.currentEquation = this.lastResult;
                        this.calculated = false;
                        this.hasPercentage = 0;
                        this.hasPower = 0;
                    }
                    if (message.GetValue("TPPlugin.Calculator.Actions.AddOperator.Data.List") == "%")
                        this.hasPercentage++;

                    if (message.GetValue("TPPlugin.Calculator.Actions.AddOperator.Data.List") == "^")
                        this.hasPower++;

                    this.currentEquation += message.GetValue("TPPlugin.Calculator.Actions.AddOperator.Data.List") ?? "";
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;


                case "TPPlugin.Calculator.Actions.AddCustomValue":
                    if (calculated)
                    {
                        this.currentEquation = "";
                        this.calculated = false;
                        this.hasPercentage = 0;
                        this.hasPower = 0;
                    }
                    if (message.GetValue("TPPlugin.Calculator.Actions.AddCustomValue.Data.List")[0] == '-' && (this.currentEquation[this.currentEquation.Length - 1] == '+' || this.currentEquation[this.currentEquation.Length - 1] == '-' || this.currentEquation[this.currentEquation.Length - 1] == '/' || this.currentEquation[this.currentEquation.Length - 1] == '*'))
                    {
                        this.currentEquation += "(";
                        this.currentEquation += message.GetValue("TPPlugin.Calculator.Actions.AddCustomValue.Data.List") ?? "";
                        this.currentEquation += ")";
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                        break;
                    }
                    this.currentEquation += message.GetValue("TPPlugin.Calculator.Actions.AddCustomValue.Data.List") ?? "";
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;


                case "TPPlugin.Calculator.Actions.AddFromHistory":
                    Console.WriteLine(message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Type.Data.List"));
                    Console.WriteLine(message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Number.Data.List"));
                    if (calculated)
                    {
                        this.currentEquation = this.lastResult;
                        this.calculated = false;
                        this.hasPercentage = 0;
                        this.hasPower = 0;
                    }
                    if (message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Type.Data.List") == "Result")
                    {
                        try
                        {
                            this.currentEquation += historyMatrix[0, Convert.ToInt32(message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Number.Data.List") ?? "0") - 1];
                        }
                        catch { }
                    }
                    else if (message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Type.Data.List") == "Equation")
                    {
                        try
                        {
                            this.currentEquation += historyMatrix[1, Convert.ToInt32(message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Number.Data.List") ?? "0") - 1];
                            hasPercentage = 0;
                            hasPower = 0;
                            for (int i = 0; i < currentEquation.Length; i++)
                            {
                                if (currentEquation[i] == '%')
                                    hasPercentage++;
                                if (currentEquation[i] == '^')
                                    hasPower++;
                            }
                        }
                        catch { }
                    }
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;


                case "TPPlugin.Calculator.Actions.Calculate":
                    if (this.hasPercentage > 0 && this.hasPower > 0)
                    {
                        this.lastResult = ReturnResult(PercentageFix(PowerFix(this.currentEquation)));
                        this.calculated = true;
                        _client.StateUpdate("TPPlugin.Calculator.States.Result", this.lastResult);
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.lastResult);
                        if (lastResult != "Syntax Error")
                        {
                            for (int i = historyMatrix.GetLength(1) - 1; i > 0; i--)
                            {
                                historyMatrix[0, i] = historyMatrix[0, i - 1];
                                historyMatrix[1, i] = historyMatrix[1, i - 1];
                            }
                            historyMatrix[0, 0] = lastResult;
                            historyMatrix[1, 0] = currentEquation;
                            for (int i = 0; i < historyMatrix.GetLength(1); i++)
                            {
                                if (historyMatrix[0, i] != null)
                                {
                                    _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                    _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
                                }
                            }
                        }
                        break;
                    }
                    else if (this.hasPercentage > 0)
                    {
                        this.lastResult = ReturnResult(PercentageFix(this.currentEquation));
                        this.calculated = true;
                        _client.StateUpdate("TPPlugin.Calculator.States.Result", this.lastResult);
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.lastResult);
                        if (lastResult != "Syntax Error")
                        {
                            for (int i = historyMatrix.GetLength(1) - 1; i > 0; i--)
                            {
                                historyMatrix[0, i] = historyMatrix[0, i - 1];
                                historyMatrix[1, i] = historyMatrix[1, i - 1];
                            }
                            historyMatrix[0, 0] = lastResult;
                            historyMatrix[1, 0] = currentEquation;
                            for (int i = 0; i < historyMatrix.GetLength(1); i++)
                            {
                                if (historyMatrix[0, i] != null)
                                {
                                    _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                    _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
                                }
                            }
                        }
                        break;
                    }
                    else if (this.hasPower > 0)
                    {
                        this.lastResult = ReturnResult(PowerFix(this.currentEquation));
                        this.calculated = true;
                        _client.StateUpdate("TPPlugin.Calculator.States.Result", this.lastResult);
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.lastResult);
                        if (lastResult != "Syntax Error")
                        {
                            for (int i = historyMatrix.GetLength(1) - 1; i > 0; i--)
                            {
                                historyMatrix[0, i] = historyMatrix[0, i - 1];
                                historyMatrix[1, i] = historyMatrix[1, i - 1];
                            }
                            historyMatrix[0, 0] = lastResult;
                            historyMatrix[1, 0] = currentEquation;
                            for (int i = 0; i < historyMatrix.GetLength(1); i++)
                            {
                                if (historyMatrix[0, i] != null)
                                {
                                    _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                    _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
                                }
                            }
                        }
                        break;
                    }

                    this.lastResult = ReturnResult(this.currentEquation);
                    this.calculated = true;

                    _client.StateUpdate("TPPlugin.Calculator.States.Result", this.lastResult);
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.lastResult);
                    if (lastResult != "Syntax Error") 
                    { 
                        for (int i = historyMatrix.GetLength(1) - 1; i > 0; i--)
                        {
                            historyMatrix[0, i] = historyMatrix[0, i - 1];
                            historyMatrix[1, i] = historyMatrix[1, i - 1];
                        }
                        historyMatrix[0, 0] = lastResult;
                        historyMatrix[1, 0] = currentEquation;
                        for (int i = 0; i < historyMatrix.GetLength(1); i++)
                        {
                            if (historyMatrix[0, i] != null)
                            {
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
                            }
                        } 
                    }
                    break;


                case "TPPlugin.Calculator.Actions.AddLastResult":
                    if (calculated)
                    {
                        this.currentEquation = "";
                        this.calculated = false;
                        this.hasPercentage = 0;
                        this.hasPower = 0;
                    }
                    this.currentEquation += this.lastResult;
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;


                case "TPPlugin.Calculator.Actions.ClearAll":
                    this.calculated = false;
                    this.lastResult = "";
                    this.currentEquation = "";
                    this.hasPercentage = 0;
                    this.hasPower = 0;
                    for (int i = 0; i < historyMatrix.GetLength(1); i++)
                    {
                        historyMatrix[1, i] = "";
                        historyMatrix[0, i] = "";
                        _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), "");
                        _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), "");
                    }
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", "");
                    _client.StateUpdate("TPPlugin.Calculator.States.Result", "");
                    break;

                
                case "TPPlugin.Calculator.Actions.ClearEquation":
                    this.calculated = false;
                    this.currentEquation = "";
                    this.hasPercentage = 0;
                    this.hasPower = 0;

                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", "");
                    break;

                
                case "TPPlugin.Calculator.Actions.BackSpace":
                    try
                    {
                        if (this.currentEquation[this.currentEquation.Length - 1] == '%')
                            this.hasPercentage--;

                        if (this.currentEquation[this.currentEquation.Length - 1] == '^')
                            this.hasPower--;

                        this.calculated = false;
                        this.currentEquation = this.currentEquation.Remove(this.currentEquation.Length - 1, 1);
                        
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);

                        break;
                    }
                    catch
                    {
                        this.calculated = false;
                        this.currentEquation = "";
                        this.hasPercentage = 0;
                        this.hasPower = 0;
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                        break;
                    }

                case "TPPlugin.Calculator.Actions.MemoryActions":
                    if (message.GetValue("TPPlugin.Calculator.Actions.MemoryAction.Data.List") == "Clear")
                    {
                        this.memoryArr[currentlySelectedMemory-1] = "";
                        _client.StateUpdate("TPPlugin.Calculator.States.MemoryValue." + currentlySelectedMemory.ToString(), this.memoryArr[currentlySelectedMemory - 1]);
                    }
                    
                    else if (message.GetValue("TPPlugin.Calculator.Actions.MemoryAction.Data.List") == "Clear All")
                    {
                        for (int i = 0; i < this.memoryArr.Length; i++)
                        {
                            this.memoryArr[i] = "";
                            _client.StateUpdate("TPPlugin.Calculator.States.MemoryValue." + (i+1).ToString(), this.memoryArr[i]);
                        }
                    }

                    else if (message.GetValue("TPPlugin.Calculator.Actions.MemoryAction.Data.List") == "Plus")
                    {
                        if (lastResult != "Syntax Error")
                        {
                            this.memoryArr[currentlySelectedMemory - 1] = ReturnResult(memoryArr[currentlySelectedMemory - 1] + "+" + lastResult);
                            _client.StateUpdate("TPPlugin.Calculator.States.MemoryValue." + currentlySelectedMemory.ToString(), this.memoryArr[currentlySelectedMemory - 1]);
                        }
                    }
                    
                    else if (message.GetValue("TPPlugin.Calculator.Actions.MemoryAction.Data.List") == "Minus")
                    {
                        if (lastResult != "Syntax Error")
                        {
                            this.memoryArr[currentlySelectedMemory - 1] = ReturnResult(memoryArr[currentlySelectedMemory - 1] + "-" + lastResult);
                            _client.StateUpdate("TPPlugin.Calculator.States.MemoryValue." + currentlySelectedMemory.ToString(), this.memoryArr[currentlySelectedMemory - 1]);
                        }
                    }
                    
                    else if (message.GetValue("TPPlugin.Calculator.Actions.MemoryAction.Data.List") == "Recall")
                    {
                        if (calculated)
                        {
                            this.currentEquation = "";
                            this.calculated = false;
                            this.hasPercentage = 0;
                            this.hasPower = 0;
                        }
                        this.currentEquation += memoryArr[currentlySelectedMemory - 1];
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    }
                    break;


                case "TPPlugin.Calculator.Actions.MemorySelect":
                    if (memoryAmount >= Convert.ToInt32(message.GetValue("TPPlugin.Calculator.Actions.MemorySelect.Number.Data.List")))
                        currentlySelectedMemory = Convert.ToInt32(message.GetValue("TPPlugin.Calculator.Actions.MemorySelect.Number.Data.List"));
                    _client.StateUpdate("TPPlugin.Calculator.States.Memory.CurrentlySelectedMemory", this.currentlySelectedMemory.ToString());
                    break;

                default:
                    var data = string.Join(", ", message.Data.Select(dataItem => $"\"{dataItem.Id}\":\"{dataItem.Value}\""));
                    Console.WriteLine($"[OnAction] PressState: {message.GetPressState()}, ActionId: {message.ActionId}, Data: '{data}'");
                    break;
            }
        }

        public void OnUnhandledEvent(string jsonMessage)
        {
            var jsonDocument = JsonSerializer.Deserialize<JsonDocument>(jsonMessage);
           Console.WriteLine($"Unhandled message: {jsonDocument}");
        }
        private string ReturnResult(string equation)
        {
            try
            {
                Console.WriteLine(compiler.Compute(equation, null).ToString());
                return Math.Round(Convert.ToDouble(compiler.Compute(equation, null)),3).ToString();
            }
            catch
            {
                Console.WriteLine("Syntax Error");
                return "Syntax Error";
            }
            
        }
        private string PercentageFix(string equation)
        {
            for (int i = 0; i < this.hasPercentage; i++)
            {
                bracketAmount = 0;
                afterBracketAmount = 0;
                for (int j = 0; j < equation.Length; j++)
                {
                    if (equation[j] == '(')
                        bracketAmount--;
                    else if (equation[j] == ')')
                        bracketAmount++;
                    if(equation[j] == '%')
                    {
                        currentPosition = j;
                        for (int x = j; x >= 0; x--)
                        {
                            if (equation[x] == '+' || equation[x] == '-' || equation[x] == '/' || equation[x] == '*')
                            {
                                startOfValue = x;
                                break;
                            }
                        }
                        break;
                    }
                }
                
                try
                {
                    if (bracketAmount == 0)
                    {
                        beforePercentage = equation.Substring(0, startOfValue + 1);
                        percentage = equation.Substring(startOfValue + 1, currentPosition - startOfValue - 1);
                        afterPercentage = equation.Substring(currentPosition + 1, equation.Length - 1 - currentPosition);
                    }
                    else
                    {
                        beforePercentage = equation.Substring(0, startOfValue + 1);
                        percentage = equation.Substring(startOfValue + 1, currentPosition - startOfValue - 1);
                        afterPercentage = equation.Substring(currentPosition + 1, equation.Length - 1 - currentPosition);
                        actualNumber = beforePercentage.Remove(beforePercentage.Length - 1, 1);
                        for (int j = 0; j < Math.Abs(bracketAmount); j++)
                        {
                            actualNumber += ")";
                        }
                        afterBracketAmount = Math.Abs(bracketAmount);
                        for (int j = 0; j < actualNumber.Length; j++)
                        {
                            if (actualNumber[j] == '(' && bracketAmount == -1)
                            {
                                startOfValue = j;
                                break;
                            }
                            else if (actualNumber[j] == '(')
                                bracketAmount++;
                        }
                        actualNumber = actualNumber.Substring(startOfValue, actualNumber.Length-startOfValue);
                        if (afterBracketAmount > 1)
                            actualNumber = actualNumber.Remove(actualNumber.Length - afterBracketAmount+1, afterBracketAmount-1);
                        Console.WriteLine(afterBracketAmount);
                        Console.WriteLine(actualNumber);
                    }
                }
                catch
                {
                    return "Syntax Error";
                }
                
                if (percentage.Length == 1)
                    percentage = "0.0" + percentage;
                else if (percentage.Length == 2)
                    percentage = "0." + percentage;
                else if (percentage.Length > 2)
                    percentage = percentage.Substring(0, percentage.Length - 2) + "." + percentage.Substring(percentage.Length - 2, 2);
                
                
                try
                {
                    if (bracketAmount == 0)
                        percentage = (Convert.ToDouble(compiler.Compute(beforePercentage.Remove(beforePercentage.Length - 1, 1), null)) * Convert.ToDouble(compiler.Compute(percentage, null))).ToString();
                    else
                        percentage = (Convert.ToDouble(compiler.Compute(actualNumber, null)) * Convert.ToDouble(compiler.Compute(percentage, null))).ToString();

                }
                catch
                {
                    return "Syntax Error";
                }
                
                equation = beforePercentage + percentage + afterPercentage;
            }
            return equation;
        }
        private string PowerFix(string equation)
        {
            for (int i = 0; i < hasPower; i++)
            {
                for (int j = 0; j < equation.Length; j++)
                {
                    if(equation[j] == '^')
                    {
                        startOfValue = 0;
                        currentPosition = j;
                        bracketAmount = 0;
                        afterBracketAmount = 0;
                        if (equation[j - 1] == ')')
                        {
                            bracketAmount = -1;
                            for (int x = j - 2; x >= 0; x--)
                            {
                                if (equation[x] == ')')
                                    bracketAmount--;
                                else if (equation[x] == '(' && bracketAmount == -1)
                                {
                                    startOfValue = x;
                                    break;
                                }
                                else if (equation[x] == '(')
                                    bracketAmount++;
                            }
                        }
                        else
                        {
                            for (int x = j; x >= 0; x--)
                            {
                                if (equation[x] == '+' || equation[x] == '-' || equation[x] == '/' || equation[x] == '*'|| equation[x] == '(')
                                {
                                    startOfValue = x;
                                    break;
                                }
                            }
                        }
                        powerEnd = equation.Length;
                        if (equation[j + 1] == '(')
                        {
                            afterBracketAmount = -1;
                            for (int x = j+2; x < equation.Length; x++)
                            {
                                if (equation[x] == '(')
                                    afterBracketAmount--;
                                else if (equation[x] == ')' && afterBracketAmount == -1)
                                {
                                    powerEnd = x;
                                    break;
                                }
                                else if (equation[x] == ')')
                                    afterBracketAmount++;
                            }
                        }
                        else
                        {
                            for (int x = j; x < equation.Length; x++)
                            {
                                if (equation[x] == '+' || equation[x] == '-' || equation[x] == '/' || equation[x] == '*' || equation[x] == ')')
                                {
                                    powerEnd = x;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
                
                try
                {
                    if (startOfValue != 0)
                    {
                        if (bracketAmount == 0)
                        {
                            beforePercentage = equation.Substring(0, startOfValue + 1);
                            actualNumber = equation.Substring(startOfValue + 1, currentPosition - startOfValue - 1);
                        }
                        else
                        {
                            beforePercentage = equation.Substring(0, startOfValue);
                            actualNumber = equation.Substring(startOfValue, currentPosition - startOfValue);
                        }

                    }
                    else
                    {
                        beforePercentage = "";
                        actualNumber = equation.Substring(startOfValue, currentPosition - startOfValue);
                    }
                        
                    if (powerEnd != equation.Length)
                    {
                        if (afterBracketAmount == 0)
                        {
                            actualPower = equation.Substring(currentPosition + 1, powerEnd - currentPosition - 1);
                            afterPercentage = equation.Substring(powerEnd, equation.Length - powerEnd);
                        }
                        else
                        {
                            actualPower = equation.Substring(currentPosition + 1, powerEnd - currentPosition);
                            Console.WriteLine(actualPower);
                            actualPower = ReturnResult(actualPower);
                            afterPercentage = equation.Substring(powerEnd+1, equation.Length - powerEnd-1);
                        }
                    }
                    else
                    {
                        actualPower = equation.Substring(currentPosition + 1, equation.Length - currentPosition - 1);
                        afterPercentage = "";
                    }
                }
                catch
                {
                    return "Syntax Error";
                }

                try
                {
                    percentage = Math.Pow(Convert.ToDouble(compiler.Compute(actualNumber, null)), Convert.ToDouble(actualPower)).ToString();
                }
                catch
                {   
                    return "Syntax Error";
                }
                equation = beforePercentage + percentage + afterPercentage;
            }
            return equation;
        }
    }
}
