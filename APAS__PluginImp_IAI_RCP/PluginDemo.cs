using APAS.Plugin.IAI.RCP.Views;
using APAS.Plugin.Sdk.Base;
using APAS.ServiceContract.Wcf;
using IAI_PCON_Controler;
using System;
using System.Configuration;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace APAS.Plugin.IAI.RCP
{
    /// <inheritdoc />
    public class PluginDemo : PluginMeasurableEquipment
    {
        #region Variables

        public event EventHandler OnCommShot;

        private readonly object _locker = new object();

        private const string PATTEN_CONTROL_PARAM_CLAMP = @"^CLAMP$";
        private const string PATTEN_CONTROL_PARAM_RELEASE = @"^RELEASE$"; 
        private const string PATTEN_CONTROL_PARAM_HOME= @"^HOME$";

        private const string CFG_NAME_PORT_NUM = "PORT";
        private const string CFG_NAME_AXIS_NUM = "AXIS";
        private const string CFG_NAME_POS_CLAMP = "POS_CLAMP";
        private const string CFG_NAME_POS_RELEASE = "POS_RELEASE";
        
        /// <summary>
        /// how long it takes to wait between the two sampling points.
        /// </summary>
        private readonly int _pollingIntervalMs = 200;

        private bool _isInit;
        private readonly Configuration _config;
        private readonly int _portNum;
        private readonly int _axisNum;
        private readonly int _posIdRelease;
        private readonly int _posIdClamp;

        #endregion

        #region Constructors

        public PluginDemo(ISystemService apasService) : base(Assembly.GetExecutingAssembly(), apasService)
        {
            #region Configuration Reading

            _config = GetAppConfig();

            LoadConfigItem(_config, "ReadIntervalMillisec", out _pollingIntervalMs, 200);

            LoadConfigItem(_config, CFG_NAME_PORT_NUM, out _portNum, 0);
            LoadConfigItem(_config, CFG_NAME_AXIS_NUM, out _axisNum, 0);
            LoadConfigItem(_config, CFG_NAME_POS_CLAMP, out _posIdClamp, 1);
            LoadConfigItem(_config, CFG_NAME_POS_RELEASE, out _posIdRelease, 0);

            #endregion

            UserView = new PluginDemoView
            {
                DataContext = this
            };

            HasView = true;
        }

        #endregion

        #region Properties

        public override string Caption => "RCP电夹";

        public override string ShortCaption => "RCP";

        public override string Description => "RCP电夹手控制插件";

        public override bool IsInitialized
        {
            get => _isInit;
            protected set => SetProperty(ref _isInit, value);
        }

        #endregion

        #region Methods

        public sealed override async Task<object> Execute(object args)
        {
            await Task.CompletedTask;
            return null;
        }

        /// <summary>
        /// Switch to the specific channel.
        /// </summary>
        /// <param name="param">[int] The specific channel.</param>
        /// <returns></returns>
        public sealed override async Task Control(string param)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("RCP控制器未初始化。");

            if (Regex.IsMatch(param, PATTEN_CONTROL_PARAM_RELEASE)) 
            {
                if (cIAI_PCON_Axis.SetDirectPositionStartSignal(_portNum, _axisNum, _posIdRelease, out var errCode) != 0)
                    return;
                throw new Exception($"夹手释放错误，错误代码{errCode}");

            }
            else if (Regex.IsMatch(param, PATTEN_CONTROL_PARAM_CLAMP)) // "OFF"
            {
                if (cIAI_PCON_Axis.SetDirectPositionStartSignal(_portNum, _axisNum, _posIdClamp, out var errCode) != 0)
                    return;
                throw new Exception($"夹手夹紧错误，错误代码{errCode}");
            }
            else if (Regex.IsMatch(param, PATTEN_CONTROL_PARAM_HOME)) // "SET LD CURR"
            {
                if (cIAI_PCON_Axis.SetHomeReturnSignal(_portNum, _axisNum, out var errCode) == 1)
                {
                    int returnCompleteStatus;
                    do
                    {
                        returnCompleteStatus = cIAI_PCON_Axis.GetHomeReturnCompleteStatus(_portNum, _axisNum, out errCode);
                        if (errCode != 0)
                        {
                            throw new Exception($"回零错误，错误代码{errCode}");
                        }

                        Thread.Sleep(100);
                    } while (returnCompleteStatus == 0);
                }
                else
                    throw new Exception($"回零错误，错误代码{errCode}"); 
            }
            else
            {
                throw new ArgumentException($"无效的控制参数 [{param}]，请查看Usage以获取有效的参数列表。");
            }

            await Task.CompletedTask;
        }

        public override void Dispose()
        {
            try
            {
                cIAI_PCON_Axis.ComPortClose(_portNum, out var errCode);
            }
            catch(Exception)
            {
                // Ignore
            }
        }
        
        public override object Fetch()
        {
            throw new NotSupportedException();
        }

        public override object DirectFetch()
        {
            return Fetch();
        }

        public override bool Init()
        {
            try
            {
                base.IsInitialized= false;
                IsEnabled = false;

                if (cIAI_PCON_Axis.ComPortOpen(_portNum, out var errCode) != 1)
                    throw new Exception($"打开串口错误，错误代码{errCode}");

                if (cIAI_PCON_Axis.SetPIOModbusSwitch(_portNum, _axisNum, 1, out errCode) == 1)
                {
                    if (cIAI_PCON_Axis.SetAlarmClearSignal(_portNum, _axisNum, out errCode) == 0)
                        throw new Exception($"清除报警错误，错误代码{errCode}");

                    if(cIAI_PCON_Axis.SetServoOnSwitch(_portNum, _axisNum, 1, out errCode) != 1)
                        throw new Exception($"伺服使能错误，错误代码{errCode}");
                }
                else
                    throw new Exception($"设置Modbus开关错误，错误代码{errCode}");

                base.IsInitialized = true;
                IsEnabled = true;

                return true;
            }
            catch (Exception)
            {
                throw;
            }

        }

        public override void StartBackgroundTask()
        {
            // Do nothing}
        }

        public override void StopBackgroundTask()
        {
            // Do nothing
        }

        #endregion

        #region Private Methods

        #endregion

        #region Commands

        /// <summary>
        /// Re-connect to the keithley 2602B
        /// </summary>
        public RelayCommand ReConnCommand
        {
            get
            {
                return new RelayCommand(x =>
                {
                    try
                    {
                        Init();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法连接RCP控制器，{ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        /// <summary>
        /// 夹手释放
        /// </summary>
        public RelayCommand GripperHomeCommand
        {
            get
            {
                return new RelayCommand(ch =>
                {
                    try
                    {
                        Control($"HOME").Wait();
                    }
                    catch (AggregateException ex)
                    {
                        var errMsg = new StringBuilder();
                        ex.Flatten().Handle(e =>
                        {
                            errMsg.AppendLine(e.Message);
                            return true;
                        });

                        MessageBox.Show($"无法归零夹手，{errMsg.ToString()}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法归零夹手，{ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        /// <summary>
        /// 夹手释放
        /// </summary>
        public RelayCommand GripperReleaseCommand
        {
            get
            {
                return new RelayCommand(ch =>
                {
                    try
                    {
                        Control($"RELEASE").Wait();
                    }
                    catch (AggregateException ex)
                    {
                        var errMsg = new StringBuilder();
                        ex.Flatten().Handle(e =>
                        {
                            errMsg.AppendLine(e.Message);
                            return true;
                        });

                        MessageBox.Show($"无法释放夹手，{errMsg.ToString()}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法释放夹手，{ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        /// <summary>
        /// 夹手夹紧
        /// </summary>
        public RelayCommand GripperClampCommand
        {
            get
            {
                return new RelayCommand(ch =>
                {
                    try
                    {
                        Control($"CLAMP").Wait();
                    }
                    catch(AggregateException ex)
                    {
                        var errMsg = new StringBuilder();
                        ex.Flatten().Handle(e =>
                        {
                            errMsg.AppendLine(e.Message);
                            return true;
                        });

                        MessageBox.Show($"无法闭合夹手，{errMsg.ToString()}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法闭合夹手，{ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        #endregion
    }
}
