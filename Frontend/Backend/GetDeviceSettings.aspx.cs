﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Security;
using TwoFactorAuthNet;
using System.Security.Cryptography;

public partial class Backend_GetDeviceSettings : System.Web.UI.Page
{
    /// <summary>
    /// При сериализации в xml переопределяем кодировку по умолчанию, чтобы не было проблем с русскими буквами.
    /// </summary>
    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
    /// <summary>
    /// Выполняет обратное преобразование в объект заданного типа из xml
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="xml"></param>
    /// <returns></returns>
    private static T Deserialize<T>(string xml)
    {
        var xs = new XmlSerializer(typeof(T));
        return (T)xs.Deserialize(new StringReader(xml));
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
    protected void Page_Load(object sender, EventArgs e)
    {
        using (VendingModelContainer dc = new VendingModelContainer())
        {
            try
            {
                DateTime dt = DateTime.Now;
                long cdt = Convert.ToInt64(dt.ToString("yyyyMMddHHmmss"));
                string cdtstr = dt.ToString("dd.MM.yyyy HH:mm:ss");
                var waterdevices = dc.WaterDevices;
                //считываем запрос
                string encryptedrequest = Request.Form["Request"];
                byte[] encryptedrequestbytes = Convert.FromBase64String(encryptedrequest);
                string signature = Request.Form["Signature"];
                byte[] signaturebytes = Convert.FromBase64String(signature);
                string encryptedaeskey = Request.Form["AData"];
                byte[] encryptedaeskeybytes = Convert.FromBase64String(encryptedaeskey);
                string encryptediv = Request.Form["BData"];
                byte[] encryptedivbytes = Convert.FromBase64String(encryptediv);
                //инициализируем криптодвижок для расшифровки
                CspParameters cspParams = new CspParameters
                {
                    ProviderType = 1
                };
                RSACryptoServiceProvider rsaProvider = new RSACryptoServiceProvider(cspParams);
                CryptoHelper ch = new CryptoHelper();
                //расшифровываем симметричный ключ и вектор инициализации
                byte[] AESKeyBytes = ch.DecryptData(encryptedaeskeybytes);
                byte[] AESIVBytes = ch.DecryptData(encryptedivbytes);
                AesCryptoServiceProvider AESProv = new AesCryptoServiceProvider
                {
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.PKCS7,
                    KeySize = 128,
                    Key = AESKeyBytes,
                    IV = AESIVBytes
                };
                //расшифровываем запрос
                string plaintext = "";
                MemoryStream memoryStream = null;
                try
                {
                    memoryStream = new MemoryStream(encryptedrequestbytes);
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, AESProv.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        plaintext = new StreamReader(cryptoStream, Encoding.UTF8).ReadToEnd();
                    }
                }
                finally
                {
                    if (memoryStream != null)
                        memoryStream.Dispose();
                }
                //byte[] plaintextbytes = rsaProvider.Decrypt(encryptedrequestbytes, false);
                long devid = Deserialize<long>(plaintext);
                //инициализируем криптодвижок для проверки подписи присланных данных
                rsaProvider = new RSACryptoServiceProvider();
                var tmpdev = dc.WaterDevices.First(x => x.ID == devid && x.Valid);
                rsaProvider.ImportCspBlob(tmpdev.PublicKey);
                bool signcorrect = rsaProvider.VerifyData(Encoding.UTF8.GetBytes(plaintext), CryptoConfig.MapNameToOID("SHA512"), signaturebytes);
                if (signcorrect)
                {
                    DeviceSettings tmp = new DeviceSettings
                    {
                        CustomerServiceContactPhone = tmpdev.CustomerServiceContactPhone,
                        PRICE_PER_ITEM_MDE = tmpdev.PRICE_PER_ITEM_MDE,
                        ProductName = tmpdev.ProductName,
                        SettingsVersion = tmpdev.SettingsVersion,
                        TaxSystem = tmpdev.TaxSystemType,
                        TankHeigthcm = tmpdev.WaterTankHeigthcm,
                        Latitude = (double)tmpdev.LocationLatitude,
                        Longitude = (double)tmpdev.LocationLongtitude,
                        UseKKT = tmpdev.UseKKT,
                        WaterTempSensorAddress = tmpdev.WaterTempSensorAddress
                    };
                    var xs = new XmlSerializer(tmp.GetType());
                    var xml = new Utf8StringWriter();
                    xs.Serialize(xml, tmp);
                    Response.Write(xml.ToString());
                }
                //сохраняем изменения в БД
            }
            catch /*(Exception ex)*/
            {


            }
            finally
            {

            }
        }
    }
}