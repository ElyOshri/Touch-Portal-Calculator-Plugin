using System;
using System.Linq;
using System.Text.Json;
using TouchPortalSDK.Interfaces;
using TouchPortalSDK.Messages.Events;
using TouchPortalSDK;
using System.Data;

namespace Calculator_Plugin
{
    public class Client : ITouchPortalEventHandler
    {
        public string PluginId => "TPCalculator";

        private readonly ITouchPortalClient _client;

        private DataTable compiler = new DataTable();
        private string currentEquation = "", lastResult = "", beforePercentage = "", afterPercentage = "", percentage = "", actualPower = "", actualNumber = "";
        private int currentPosition = 0, startOfValue = 0, hasPercentage = 0, hasPower = 0, powerEnd = 0, bracketAmount = 0, currentlySelectedMemory = 1;
        private bool calculated = false;
        string[,] historyMatrix = { { "", "", "", "", "" }, { "", "", "", "", "" } };
        string[] memoryArr = { "", "", "", "", "" };
        
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

        /// <summary>
        /// Information received when plugin is connected to TouchPortal.
        /// </summary>
        /// <param name="message"></param>
        public void OnInfoEvent(InfoEvent message)
        {
            Console.WriteLine($"[Info] VersionCode: '{message.TpVersionCode}', VersionString: '{message.TpVersionString}', SDK: '{message.SdkVersion}', PluginVersion: '{message.PluginVersion}', Status: '{message.Status}'");
        }

        /// <summary>
        /// User selected an item in a dropdown menu in the TouchPortal UI.
        /// </summary>
        /// <param name="message"></param>
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
            //_settings = message.Values;
            Console.WriteLine($"[OnSettings] Settings: {message}");
        }

        /// <summary>
        /// User clicked an action.
        /// </summary>
        /// <param name="message"></param>
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
                        this.currentEquation += historyMatrix[0, Convert.ToInt32(message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Number.Data.List") ?? "0") - 1];
                    }
                    else if (message.GetValue("TPPlugin.Calculator.Actions.AddFromHistory.Type.Data.List") == "Equation")
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
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
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
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
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
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                                _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
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
                            _client.StateUpdate("TPPlugin.Calculator.States.History.Result." + (i + 1).ToString(), historyMatrix[0, i]);
                            _client.StateUpdate("TPPlugin.Calculator.States.History.Equation." + (i + 1).ToString(), historyMatrix[1, i]);
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
                        for (int i = 0; i < 5; i++)
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
                        this.currentEquation += memoryArr[currentlySelectedMemory - 1];
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    }
                    break;


                case "TPPlugin.Calculator.Actions.MemorySelect":
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
                return compiler.Compute(equation, null).ToString();
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
                for (int j = 0; j < equation.Length; j++)
                {
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
                    beforePercentage = equation.Substring(0, startOfValue+1);
                    percentage = equation.Substring(startOfValue+1, currentPosition - startOfValue - 1);
                    afterPercentage = equation.Substring(currentPosition+1, equation.Length-1 - currentPosition);
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
                    percentage = (Convert.ToDouble(compiler.Compute(beforePercentage.Remove(beforePercentage.Length - 1, 1), null)) * Convert.ToDouble(compiler.Compute(percentage, null))).ToString();
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
                                if (equation[x] == '+' || equation[x] == '-' || equation[x] == '/' || equation[x] == '*')
                                {
                                    startOfValue = x;
                                    break;
                                }
                            }
                        }
                        powerEnd = equation.Length;
                        for (int x = j; x < equation.Length; x++)
                        {
                            if (equation[x] == '+' || equation[x] == '-' || equation[x] == '/' || equation[x] == '*'|| equation[x] == ')')
                            {
                                powerEnd = x;
                                break;
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
                        actualPower = equation.Substring(currentPosition + 1, powerEnd - currentPosition - 1);
                        afterPercentage = equation.Substring(powerEnd, equation.Length - powerEnd );
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
