using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace bluetooth
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            timer.Tick += Timer_Tick;
        }

        #region  实现蓝牙
        private async void scanBlueToothDevice()
        {
            deviceList.Items.Clear();
            await ScanDevice();
        }

        //连接蓝牙设备
        private async void Accept()
        {
            try
            {
                if (await ConnectToDevice() == false)
                    return;
                if (await FindService() == false)
                    return;
                if (await FindCharacteristic() == false)
                    return;
                getValue();
            }
            catch (Exception exc)
            {
                Debug.WriteLine("BindDevicePage ABB_Accept_OnClicked: " + exc.Message);
            }

            MessageDialog dialog = new MessageDialog("Bind Device successfully!");
            dialog.Commands.Add(new UICommand("OK", cmd => { }, commandId: 0));
            IUICommand result = await dialog.ShowAsync();
            if ((int)result.Id == 0)
            {
                if (this.Frame.CanGoBack)
                {
                    this.Frame.GoBack();
                }
            }
        }

        private async void sendMessage(byte[] bytes)
        {
            if (_characteristic != null)
            {
                try
                {
                    DataWriter d = new DataWriter();
                    d.WriteBytes(bytes);
                   
                    //await _characteristic.WriteValueAsync(d.DetachBuffer());
                    await _characteristic.WriteValueAsync(bytes.AsBuffer());
                    setCount(bytes.Length, 0);
                }
                catch (Exception e) {
                    await new MessageDialog("sendMessage error:"+e.Message).ShowAsync();
                }
                //if (status == GattCommunicationStatus.Success) { setCount(bytes.Length, 0); }
            }
        }

        DeviceInformationCollection bleDevices;
        BluetoothLEDevice _device;
        IReadOnlyList<GattDeviceService> _services;
        GattDeviceService _service;
        GattCharacteristic _characteristic;

        public string str;
        bool _notificationRegistered;
        List<byte[]> recList = new List<byte[]>();
        private async Task<int> ScanDevice()
        {
            try
            {
                //查找所有设备
                bleDevices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector());

                Debug.WriteLine("Found " + bleDevices.Count + " device(s)");

                if (bleDevices.Count == 0)
                {
                    await new MessageDialog("No BLE Devices found - make sure you've paired your device").ShowAsync();
                    //跳转到蓝牙设置界面
                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-bluetooth:", UriKind.RelativeOrAbsolute));
                }
                for(int i=0;i<bleDevices.Count;i++)
                {
                    deviceList.Items.Add(bleDevices[i].Name);
                }
                
                //deviceList.ItemsSource = bleDevices;
            }
            catch (Exception ex)
            {
                //if((uint)ex.HResult == 0x8007048F)
                //{
                //    await new MessageDialog("Bluetooth is turned off!").ShowAsync();
                //}
                await new MessageDialog("Failed to find BLE devices: " + ex.Message).ShowAsync();
            }

            return bleDevices.Count;
        }
        private async Task<bool> ConnectToDevice()
        {
            try
            {
                for (int i = 0; i < bleDevices.Count; i++)
                {
                    //VICTOR是设备名称，改成你自己的就行
                    if (bleDevices[i].Name == "Find Me Target")
                    {
                        _device = await BluetoothLEDevice.FromIdAsync(bleDevices[i].Id);
                        //_service= await GattDeviceService.FromIdAsync(bleDevices[i].Id);  
                        _services = _device.GattServices;
                        Debug.WriteLine("Found Device: " + _device.Name);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog("Connection failed: " + ex.Message).ShowAsync();
                return false;
            }
            await new MessageDialog("Unable to find device VICTOR - has it been paired?").ShowAsync();

            return true;
        }

       

        private async Task<bool> FindService()
        {
            foreach (GattDeviceService s in _services)
            {
                Debug.WriteLine("Found Service: " + s.Uuid);
                if (s.Uuid == new Guid("00000000-0000-1000-8000-00805f9b34fb"))
                {
                    _service = s;
                    return true;
                }
            }
            await new MessageDialog("Unable to find VICTOR Service 0000fff0").ShowAsync();
            return false;
        }
        private async Task<bool> FindCharacteristic()
        {
            foreach (var c in _service.GetCharacteristics(new Guid("00000001-0000-1000-8000-00805f9b34fb")))
            {
                //"unauthorized access" without proper permissions
                _characteristic = c;
                Debug.WriteLine("Found characteristic: " + c.Uuid);
               // await new MessageDialog("properties:" + _characteristic.CharacteristicProperties.ToString()).ShowAsync();
                return true;
            }
            //IReadOnlyList<GattCharacteristic> characteristics = _service.GetAllCharacteristics();

            await new MessageDialog("Could not find characteristic or permissions are incorrrect").ShowAsync();
            return false;
        }

        public async void getValue()
        {
            //注册消息监听器
            await RegisterNotificationAsync();
            GattReadResult x = await _characteristic.ReadValueAsync();

            if (x.Status == GattCommunicationStatus.Success)
            {
                str = ParseString(x.Value);
                Debug.WriteLine(str);
                
            }
        }

        public static string ParseString(IBuffer buffer)
        {
            DataReader reader = DataReader.FromBuffer(buffer);
            return reader.ReadString(buffer.Length);
        }

        //返回数组
        public static byte[] ParseBytes(IBuffer buffer)
        {
            return buffer.ToArray();
            
        }
           

        //接受蓝牙数据并校验进行上传
        public async void CharacteristicValueChanged_Handler(GattCharacteristic sender, GattValueChangedEventArgs obj)
        {
            byte[] bytes;
            CryptographicBuffer.CopyToByteArray(obj.CharacteristicValue, out bytes);
            //byte[] bytes = ParseBytes(obj.CharacteristicValue);
            
            //string str = "";
            //for (int i=0;i<bytes.Length;i++) {
            //    str += "," + bytes[i].ToString();
            //}

            //byte[] b = new byte[20];
            //for (int i = 0; i < b.Length; i++)
            //{
            //    b[i] = (byte)(i + 1);
            //}

            
            
                recCount += (UInt64)(bytes.Length);
            //endTime1 = DateTime.Now;
               await Task.Run(() => showInfo(bytes,recCount));
            //  await Task.Factory.StartNew(() => showInfo(bytes, recCount));
            //Task t = new Task(async () => await showInfo(bytes,recCount));
            //    t.Start();
            


            //await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            //{

            //    ////显示统计
            //    //if (isEqual(bytes, getSendMessage()) == true) {
            //    //    recCount += (UInt64)(bytes.Length);
            //    //    textRecCount.Text = recCount.ToString();
            //    //   // endTime1 = DateTime.Now;
            //    //   // textRecRate.Text=((int)(recCount / (endTime1 - beginTime1).TotalSeconds)).ToString()+"b/s";
            //    //}
            //    //else { textError.Text = "-1"; }


            //    //if (isEqual(bytes, b) == true) { recCount += (UInt64)(bytes.Length); textRecCount.Text = recCount.ToString(); }
            //    //else { textError.Text = "-1"; }

            //    //textRecContent.Text += str+"\r\n";
            //    //if (textRecContent.Text.Length > 1000) textRecContent.Text = "";

            //    //(Windows.UI.Xaml.Window.Current.Content as Windows.UI.Xaml.Controls.Frame).Navigate(typeof(MainPage));
            //});

            //这个不行
            //CoreDispatcher dispatcher = Window.Current.CoreWindow.Dispatcher;
            //var handler = dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            //{

            //        for (int i=0;i<bytes.Length;i++) {
            //        str +=","+ bytes[i].ToString();
            //        }
            //        textBox.Text = str;

            //});
            //TODO str就是蓝牙数据，你可以做自己想做的了

        }

        private async Task showInfo(byte[] c, UInt64 count)
        {
            if (isEqual(c) == true)
            {

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    textRecCount.Text = count.ToString();
                                //textRecRate.Text = ((int)(recCount / (endTime1 - beginTime1).TotalSeconds)).ToString() + "b/s";
                            });
            }
            else
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    textError.Text = "-1";

                });
            }
        }

        public async Task RegisterNotificationAsync()
        {
            if (_notificationRegistered)
            {
                return;
            }
            GattCommunicationStatus writeResult;
            if (((_characteristic.CharacteristicProperties & GattCharacteristicProperties.Notify) != 0))
            {
                _characteristic.ValueChanged +=
                    new TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>(CharacteristicValueChanged_Handler);
                writeResult = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                
                _notificationRegistered = true;

                beginTime1 = DateTime.Now;
                
            }
        }

        private void clearBLE() {
            if (_characteristic != null) {
                _characteristic.ValueChanged -= CharacteristicValueChanged_Handler;
                _characteristic = null;
            }
            if (_device != null) {
                _device?.Dispose();
                _device = null;
            }
        }

        #endregion

        #region 另外一种实现
        private async void findDevice()
        {  
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(GattDeviceService.GetDeviceSelectorFromUuid(GattServiceUuids.GenericAccess));
            if (devices == null) return;
            await new MessageDialog("find devices success!").ShowAsync();
            findService(devices);
        }

        private async void findService(DeviceInformationCollection devices)
        {

            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Name == "CY Custom BLE")
                {
                    await new MessageDialog("Name:"+devices[i].Name+" id:"+devices[i].Id).ShowAsync();
                    var service = await GattDeviceService.FromIdAsync(devices[i].Id);
                    if (service == null) return;
                    await new MessageDialog("find service success!").ShowAsync();
                }
            }
        }


        #endregion


        #region  界面处理

        /// <summary>
        /// 定时处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        DispatcherTimer timer=new DispatcherTimer();//定义定时器
        UInt64 sendCount = 0;
        UInt64 recCount = 0;
        DateTime beginTime, endTime;
        DateTime beginTime1, endTime1;

        private void Timer_Tick(object sender, object e) {
            byte[] bytes = getSendMessage();
            if (bytes != null) sendMessage(bytes);
        }


         private  void setCount(int sCount,int rCount) {
            sendCount =sendCount + (UInt64)sCount;
            recCount +=(UInt64)rCount;
            textRecCount.Text = recCount.ToString();
            textSendCount.Text = sendCount.ToString();

            endTime = DateTime.Now;
            textSendTime.Text = (endTime - beginTime).ToString();
        }

        private void clearInfo() {
            sendCount = 0;
            recCount = 0;
            textRecCount.Text = recCount.ToString();
            textSendCount.Text = sendCount.ToString();
            textRecContent.Text = "";
        }

        private  byte[] getSendMessage() {
            
            string[] str=textBoxSendMessage.Text.Split(' ');
            List<string> list = str.ToList();
            for (int i=0;i<list.Count;i++) {
                if (list[i] == "") list.RemoveAt(i);
            }
            str = list.ToArray();

            byte[] bytes = new byte[str.Length];
            for (int i=0;i<str.Length;i++) {
                bytes[i] = Byte.Parse(str[i]);
            }

            return bytes;
        }

        private Boolean isEqual(byte[] source ,byte[] des) {
            if (source.Length != des.Length) return false;
            for (int i=0;i<source.Length;i++) {
                if (source[i] != des[i]) return false;
            }
            return true;
        }

        private Boolean isEqual(byte[] des)
        {
            for (int i = 0; i < des.Length; i++)
            {
                if ((i+1) != des[i]) return false;
            }
            return true;
        }

        #endregion

        /// <summary>
        /// 扫描蓝牙
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_Click(object sender, RoutedEventArgs e)
        {
            scanBlueToothDevice();
            
        }
        /// <summary>
        /// 连接蓝牙
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Accept();
        }

        /// <summary>
        /// 发送信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private  void button2_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes= getSendMessage();

            //for (int i=0;i<bytes.Length;i++) {
            //    await new MessageDialog( "bytes:"+bytes[i].ToString() ).ShowAsync();
            //}

            if (bytes!=null)  sendMessage(bytes);
        }
        /// <summary>
        /// 清空信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private  void buttonClear_Click(object sender, RoutedEventArgs e)
        {
            //textRecCount.Text = recCount.ToString();
            //await new MessageDialog("总接受数据："+recCount.ToString()).ShowAsync();
           clearInfo();
            beginTime1 = DateTime.Now;
        }

        /// <summary>
        /// 循环发送
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button3_Click(object sender, RoutedEventArgs e)
        {
            byte[] bytes = new byte[20];
            for (int i=0;i<bytes.Length;i++) {
                bytes[i]= (byte)(i+1);
            }
            beginTime = DateTime.Now;
            for(int i=0;i<5000;i++) {
                if (_characteristic != null)
                {
                    GattCommunicationStatus status = await _characteristic.WriteValueAsync(bytes.AsBuffer(),GattWriteOption.WriteWithoutResponse);
                    if (status == GattCommunicationStatus.Success) { setCount(bytes.Length, 0); }
                   
                }
            }
            endTime = DateTime.Now;
            textSendTime.Text = (endTime - beginTime).ToString();
            await new MessageDialog("send complete!").ShowAsync();
        }

        private void toggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (toggleButton.IsChecked == true)
            {
                clearInfo();
                timer.Interval = new TimeSpan(0, 0, 0, 0, (int)slider.Value);
                timer.Start();
                beginTime= DateTime.Now;
            }
            else {
                timer.Stop();
                endTime = DateTime.Now;
                textSendTime.Text=(endTime- beginTime).ToString();
                //textSendTime.Text=String.Format("yyyy-MM-dd HH:mm:ss:ffff", (endTime-beginTime));
            }

        }
    }
}
