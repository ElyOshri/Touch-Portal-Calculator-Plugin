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
        
        private string currentEquation = "";
        private string lastResult;
        private bool calculated = false;
        private int hasPercentage = 0;
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
                    }
                    if (message.GetValue("TPPlugin.Calculator.Actions.AddOperator.Data.List") == "%")
                        this.hasPercentage++;
                    this.currentEquation += message.GetValue("TPPlugin.Calculator.Actions.AddOperator.Data.List") ?? "";
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;
                
                case "TPPlugin.Calculator.Actions.AddCustomValue":
                    if (calculated)
                    {
                        this.currentEquation = "";
                        this.calculated = false;
                        this.hasPercentage = 0;
                    }
                    if (message.GetValue("TPPlugin.Calculator.Actions.AddCustomValue.Data.List")[0] == '-' && (this.currentEquation[this.currentEquation.Length-1] == '+' || this.currentEquation[this.currentEquation.Length - 1] == '-' || this.currentEquation[this.currentEquation.Length - 1] == '/' || this.currentEquation[this.currentEquation.Length - 1] == '*'))
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
                
                case "TPPlugin.Calculator.Actions.Calculate":
                    if (this.hasPercentage > 0)
                    {
                        this.lastResult = ReturnResult(PercentageFix(this.currentEquation));
                        this.calculated = true;
                        _client.StateUpdate("TPPlugin.Calculator.States.Result", this.lastResult);
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.lastResult);
                        break;
                    }
                    this.lastResult = ReturnResult(this.currentEquation);
                    this.calculated = true;
                    _client.StateUpdate("TPPlugin.Calculator.States.Result", this.lastResult);
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.lastResult);
                    break;

                case "TPPlugin.Calculator.Actions.AddLastResult":
                    if (calculated)
                    {
                        this.currentEquation = "";
                        this.calculated = false;
                        this.hasPercentage = 0;
                    }
                    this.currentEquation += this.lastResult;
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                    break;

                case "TPPlugin.Calculator.Actions.ClearAll":
                    this.calculated = false;
                    this.lastResult = "";
                    this.currentEquation = "";
                    this.hasPercentage = 0;
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", "");
                    _client.StateUpdate("TPPlugin.Calculator.States.Result", "");
                    break;
                
                case "TPPlugin.Calculator.Actions.ClearEquation":
                    this.calculated = false;
                    this.currentEquation = "";
                    this.hasPercentage = 0;
                    _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", "");
                    break;
                
                case "TPPlugin.Calculator.Actions.BackSpace":
                    try
                    {
                        if (this.currentEquation[this.currentEquation.Length - 1] == '%')
                            this.hasPercentage--;
                        this.calculated = false;
                        this.currentEquation = this.currentEquation.Remove(this.currentEquation.Length - 1, 1);
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                        break;
                    }
                    catch
                    {
                        this.currentEquation = "";
                        _client.StateUpdate("TPPlugin.Calculator.States.CurrentEquation", this.currentEquation);
                        break;
                    }

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
                string value = new DataTable().Compute(equation, null).ToString();
                Console.WriteLine(value);
                return value;
            }
            catch
            {
                Console.WriteLine("Syntax Error");
                return "Syntax Error";
            }
            
        }
        private string PercentageFix(string equation)
        {
            string beforePercentage = "", afterPercentage = "", percentage = "";
            int currentPosition = 0, startOfValue = 0;
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
                
                var temp1 = new DataTable();
                double temp3 = 0, temp4 = 0;
                
                try
                {
                    temp3 = Convert.ToDouble(temp1.Compute(beforePercentage.Remove(beforePercentage.Length - 1, 1), null));
                    temp4 = Convert.ToDouble(temp1.Compute(percentage, null));
                }
                catch
                {
                    return "Syntax Error";
                }
                
                percentage = (temp3 * temp4).ToString();
                equation = beforePercentage + percentage + afterPercentage;
            }
            return equation;
        }
    }
}
