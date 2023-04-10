using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;

namespace BLEConsole
{
    /// <summary>
    ///     Represents the display of an attribute - both characteristics and services.
    /// </summary>
    public class BluetoothLEAttributeDisplay
    {
        public GattCharacteristic characteristic;
        public GattDescriptor descriptor;

        public GattDeviceService service;

        public BluetoothLEAttributeDisplay(GattDeviceService service)
        {
            this.service = service;
            AttributeDisplayType = AttributeType.Service;
        }

        public BluetoothLEAttributeDisplay(GattCharacteristic characteristic)
        {
            this.characteristic = characteristic;
            AttributeDisplayType = AttributeType.Characteristic;
        }

        public string Chars => (CanRead ? "R" : " ") + (CanWrite ? "W" : " ") + (CanNotify ? "N" : " ");

        public bool CanRead
        {
            get
            {
                return this.characteristic != null ? this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read) : false;
            }
        }

        public bool CanWrite
        {
            get
            {
                return this.characteristic != null ? 
                    (this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                     this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse) ||
                     this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.ReliableWrites) ||
                     this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WritableAuxiliaries))
                    : false;
            }
        }

        public bool CanNotify
        {
            get
            {
                return this.characteristic != null ? this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) : false;
            }
        }


        public string Name
        {
            get
            {
                switch (AttributeDisplayType)
                {
                    case AttributeType.Service:
                        if (IsSigDefinedUuid(service.Uuid))
                        {
                            ushort shortId = Utilities.ConvertUuidToShortId(service.Uuid);
                            string serviceName = Enum.GetName(typeof(GattNativeServiceUuid), shortId);
                            if (serviceName != null)
                            {
                                return serviceName;
                            }
                            return String.Format("0x{0:X4}", shortId);
                        }
                        return "Custom Service: " + service.Uuid;
                    case AttributeType.Characteristic:
                        if (IsSigDefinedUuid(characteristic.Uuid))
                        {
                            ushort shortId = Utilities.ConvertUuidToShortId(characteristic.Uuid);
                            string characteristicName = Enum.GetName(typeof(GattNativeCharacteristicUuid), shortId);
                            if (characteristicName != null)
                            {
                                return characteristicName;
                            }
                            return String.Format("0x{0:X4}", shortId);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(characteristic.UserDescription))
                            {
                                return characteristic.UserDescription;
                            }
                                
                            else
                            {
                                return "Custom Characteristic: " + characteristic.Uuid;
                            }
                        }
                        break;
                    default:
                        break;
                }
                return "Invalid";
            }
        }

        public string Uuid
        {
            get
            {
                switch (AttributeDisplayType)
                {
                    case AttributeType.Service:
                        return service.Uuid.ToString();
                    case AttributeType.Characteristic:
                        return characteristic.Uuid.ToString();
                    default:
                        break;
                }
                return "Invalid";
            }
        }

        public AttributeType AttributeDisplayType { get; }

        /// <summary>
        ///     The SIG has a standard base value for Assigned UUIDs. In order to determine if a UUID is SIG defined,
        ///     zero out the unique section and compare the base sections.
        /// </summary>
        /// <param name="uuid">The UUID to determine if SIG assigned</param>
        /// <returns></returns>
        private static bool IsSigDefinedUuid(Guid uuid)
        {
            var bluetoothBaseUuid = new Guid("00000000-0000-1000-8000-00805F9B34FB");

            var bytes = uuid.ToByteArray();
            // Zero out the first and second bytes
            // Note how each byte gets flipped in a section - 1234 becomes 34 12
            // Example Guid: 35918bc9-1234-40ea-9779-889d79b753f0
            //                   ^^^^
            // bytes output = C9 8B 91 35 34 12 EA 40 97 79 88 9D 79 B7 53 F0
            //                ^^ ^^
            bytes[0] = 0;
            bytes[1] = 0;
            var baseUuid = new Guid(bytes);
            return baseUuid == bluetoothBaseUuid;
        }
    }

    public enum AttributeType
    {
        Service = 0,
        Characteristic = 1,
        Descriptor = 2
    }

    /// <summary>
    ///     Display class used to represent a BluetoothLEDevice in the Device list
    /// </summary>
    public class BluetoothLEDeviceDisplay : INotifyPropertyChanged
    {
        public BluetoothLEDeviceDisplay(DeviceInformation deviceInfoIn)
        {
            DeviceInformation = deviceInfoIn;
        }

        public DeviceInformation DeviceInformation { get; private set; }

        public string Id => DeviceInformation.Id;
        public string Name => DeviceInformation.Name;
        public bool IsPaired => DeviceInformation.Pairing.IsPaired;
        public bool IsConnected => (bool?)DeviceInformation.Properties["System.Devices.Aep.IsConnected"] == true;
        public bool IsConnectable => (bool?)DeviceInformation.Properties["System.Devices.Aep.Bluetooth.Le.IsConnectable"] == true;

        public IReadOnlyDictionary<string, object> Properties => DeviceInformation.Properties;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Update(DeviceInformationUpdate deviceInfoUpdate)
        {
            DeviceInformation.Update(deviceInfoUpdate);

            OnPropertyChanged("Id");
            OnPropertyChanged("Name");
            OnPropertyChanged("DeviceInformation");
            OnPropertyChanged("IsPaired");
            OnPropertyChanged("IsConnected");
            OnPropertyChanged("Properties");
            OnPropertyChanged("IsConnectable");
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    ///     This enum assists in finding a string representation of a BT SIG assigned value for Service UUIDS
    ///     Assigned Numbers section 3.4.1 GATT Services by Name
    ///     Reference: https://www.bluetooth.com/specifications/assigned-numbers/
    /// </summary>
    public enum GattNativeServiceUuid : ushort
    {
        None = 0,
        AlertNotification = 0x1811,
        AudioInputControl = 0x1843,
        AudioStreamControl = 0x184E,
        AuthorizationControl = 0x183D,
        AutomationIO = 0x1815,
        BasicAudioAnnouncement = 0x1851,
        Battery = 0x180F,
        BinarySensor = 0x183B,
        BloodPressure = 0x1810,
        BodyComposition = 0x181B,
        BondManagement = 0x181E,
        BroadcastAudioAnnouncement = 0x1852,
        BroadcastAudioScan = 0x184F,
        CommonAudio = 0x1853,
        ConstantToneExtension = 0x184A,
        ContinuousGlucoseMonitoring = 0x181F,
        CoordinatedSetIdentification = 0x1846,
        CurrentTime = 0x1805,
        CyclingPower = 0x1818,
        CyclingSpeedandCadence = 0x1816,
        DeviceInformation = 0x180A,
        DeviceTime = 0x1847,
        ElectronicShelfLabel = 0x1857,
        EmergencyConfiguration = 0x183C,
        EnvironmentalSensing = 0x181A,
        FitnessMachine = 0x1826,
        GenericAccess = 0x1800,
        GenericAttribute = 0x1801,
        GenericMediaControl = 0x1849,
        GenericTelephoneBearer = 0x184C,
        Glucose = 0x1808,
        HealthThermometer = 0x1809,
        HearingAid = 0x1854,
        HeartRate = 0x180D,
        HTTPProxy = 0x1823,
        HumanInterfaceDevice = 0x1812,
        ImmediateAlert = 0x1802,
        IndoorPositioning = 0x1821,
        InsulinDelivery = 0x183A,
        InternetProtocolSupport = 0x1820,
        LinkLoss = 0x1803,
        LocationandNavigation = 0x1819,
        MediaControl = 0x1848,
        MeshProvisioning = 0x1827,
        MeshProxy = 0x1828,
        MicrophoneControl = 0x184D,
        NextDSTChange = 0x1807,
        ObjectTransfer = 0x1825,
        PhoneAlertStatus = 0x180E,
        PhysicalActivityMonitor = 0x183E,
        PublicBroadcastAnnouncement = 0x1856,
        PublishedAudioCapabilities = 0x1850,
        PulseOximeter = 0x1822,
        ReconnectionConfiguration = 0x1829,
        ReferenceTimeUpdate = 0x1806,
        RunningSpeedandCadence = 0x1814,
        ScanParameters = 0x1813,
        TelephoneBearer = 0x184B,
        TMAS = 0x1855,
        TransportDiscovery = 0x1824,
        TxPower = 0x1804,
        UserData = 0x181C,
        VolumeControl = 0x1844,
        VolumeOffsetControl = 0x1845,
        WeightScale = 0x181D,
        SimpleKeyService = 0xFFE0
    }

    /// <summary>
    ///     This enum is nice for finding a string representation of a BT SIG assigned value for Characteristic UUIDs
    ///     Assigned Numbers section 3.8.1  Characteristics by Name  Last Modified: 2023­03­30
    ///     Reference: https://www.bluetooth.com/specifications/assigned-numbers/
    /// </summary>
    public enum GattNativeCharacteristicUuid : ushort
    {
        None = 0,
        ACSControlPoint = 0x2B33,
        ACSDataIn = 0x2B30,
        ACSDataOutIndicate = 0x2B32,
        ACSDataOutNotify = 0x2B31,
        ACSStatus = 0x2B2F,
        ActivePresetIndex = 0x2BDC,
        ActivityCurrentSession = 0x2B44,
        ActivityGoal = 0x2B4E,
        AdvertisingConstantToneExtensionInterval = 0x2BB1,
        AdvertisingConstantToneExtensionMinimumLength = 0x2BAE,
        AdvertisingConstantToneExtensionMinimumTransmitCount = 0x2BAF,
        AdvertisingConstantToneExtensionPHY = 0x2BB2,
        AdvertisingConstantToneExtensionTransmitDuration = 0x2BB0,
        AerobicHeartRateLowerLimit = 0x2A7E,
        AerobicHeartRateUpperLimit = 0x2A84,
        AerobicThreshold = 0x2A7F,
        Age = 0x2A80,
        Aggregate = 0x2A5A,
        AlertCategoryID = 0x2A43,
        AlertCategoryIDBitMask = 0x2A42,
        AlertLevel = 0x2A06,
        AlertNotificationControlPoint = 0x2A44,
        AlertStatus = 0x2A3F,
        Altitude = 0x2AB3,
        AmmoniaConcentration = 0x2BCF,
        AnaerobicHeartRateLowerLimit = 0x2A81,
        AnaerobicHeartRateUpperLimit = 0x2A82,
        AnaerobicThreshold = 0x2A83,
        APSyncKeyMaterial = 0x2BF7,
        ApparentEnergy32 = 0x2B89,
        ApparentPower = 0x2B8A,
        ApparentWindDirection = 0x2A73,
        ApparentWindSpeed = 0x2A72,
        Appearance = 0x2A01,
        ASEControlPoint = 0x2BC6,
        AudioInputControlPoint = 0x2B7B,
        AudioInputDescription = 0x2B7C,
        AudioInputState = 0x2B77,
        AudioInputStatus = 0x2B7A,
        AudioInputType = 0x2B79,
        AudioLocation = 0x2B81,
        AudioOutputDescription = 0x2B83,
        AvailableAudioContexts = 0x2BCD,
        AverageCurrent = 0x2AE0,
        AverageVoltage = 0x2AE1,
        BarometricPressureTrend = 0x2AA3,
        BatteryCriticalStatus = 0x2BE9,
        BatteryEnergyStatus = 0x2BF0,
        BatteryHealthInformation = 0x2BEB,
        BatteryHealthStatus = 0x2BEA,
        BatteryInformation = 0x2BEC,
        BatteryLevel = 0x2A19,
        BatteryLevelStatus = 0x2BED,
        BatteryTimeStatus = 0x2BEE,
        BearerListCurrentCalls = 0x2BB9,
        BearerProviderName = 0x2BB3,
        BearerSignalStrength = 0x2BB7,
        BearerSignalStrengthReportingInterval = 0x2BB8,
        BearerTechnology = 0x2BB5,
        BearerUCI = 0x2BB4,
        BearerURISchemesSupportedList = 0x2BB6,
        BloodPressureFeature = 0x2A49,
        BloodPressureMeasurement = 0x2A35,
        BloodPressureRecord = 0x2B36,
        BluetoothSIGData = 0x2B39,
        BodyCompositionFeature = 0x2A9B,
        BodyCompositionMeasurement = 0x2A9C,
        BodySensorLocation = 0x2A38,
        BondManagementControlPoint = 0x2AA4,
        BondManagementFeature = 0x2AA5,
        Boolean = 0x2AE2,
        BootKeyboardInputReport = 0x2A22,
        BootKeyboardOutputReport = 0x2A32,
        BootMouseInputReport = 0x2A33,
        BR_EDRHandoverData = 0x2B38,
        BroadcastAudioScanControlPoint = 0x2BC7,
        BroadcastReceiveState = 0x2BC8,
        BSSControlPoint = 0x2B2B,
        BSSResponse = 0x2B2C,
        CallControlPoint = 0x2BBE,
        CallControlPointOptionalOpcodes = 0x2BBF,
        CallFriendlyName = 0x2BC2,
        CallState = 0x2BBD,
        CaloricIntake = 0x2B50,
        CarbonMonoxideConcentration = 0x2BD0,
        CardioRespiratoryActivityInstantaneousData = 0x2B3E,
        CardioRespiratoryActivitySummaryData = 0x2B3F,
        CentralAddressResolution = 0x2AA6,
        CGMFeature = 0x2AA8,
        CGMMeasurement = 0x2AA7,
        CGMSessionRunTime = 0x2AAB,
        CGMSessionStartTime = 0x2AAA,
        CGMSpecificOpsControlPoint = 0x2AAC,
        CGMStatus = 0x2AA9,
        ChromaticDistancefromPlanckian = 0x2AE3,
        ChromaticityCoordinate = 0x2B1C,
        ChromaticityCoordinates = 0x2AE4,
        ChromaticityinCCTandDuvValues = 0x2AE5,
        ChromaticityTolerance = 0x2AE6,
        CIE13_3_1995ColorRenderingIndex = 0x2AE7,
        ClientSupportedFeatures = 0x2B29,
        CO2Concentration = 0x2B8C,
        Coefficient = 0x2AE8,
        ConstantToneExtensionEnable = 0x2BAD,
        ContentControlID = 0x2BBA,
        CoordinatedSetSize = 0x2B85,
        CorrelatedColorTemperature = 0x2AE9,
        CosineoftheAngle = 0x2B8D,
        Count16 = 0x2AEA,
        Count24 = 0x2AEB,
        CountryCode = 0x2AEC,
        CrossTrainerData = 0x2ACE,
        CSCFeature = 0x2A5C,
        CSCMeasurement = 0x2A5B,
        CurrentGroupObjectID = 0x2BA0,
        CurrentTime = 0x2A2B,
        CurrentTrackObjectID = 0x2B9D,
        CurrentTrackSegmentsObjectID = 0x2B9C,
        CyclingPowerControlPoint = 0x2A66,
        CyclingPowerFeature = 0x2A65,
        CyclingPowerMeasurement = 0x2A63,
        CyclingPowerVector = 0x2A64,
        DatabaseChangeIncrement = 0x2A99,
        DatabaseHash = 0x2B2A,
        DateofBirth = 0x2A85,
        DateofThresholdAssessment = 0x2A86,
        DateTime = 0x2A08,
        DateUTC = 0x2AED,
        DayDateTime = 0x2A0A,
        DayofWeek = 0x2A09,
        DescriptorValueChanged = 0x2A7D,
        DeviceName = 0x2A00,
        DeviceTime = 0x2B90,
        DeviceTimeControlPoint = 0x2B91,
        DeviceTimeFeature = 0x2B8E,
        DeviceTimeParameters = 0x2B8F,
        DeviceWearingPosition = 0x2B4B,
        DewPoint = 0x2A7B,
        DSTOffset = 0x2A0D,
        ElectricCurrent = 0x2AEE,
        ElectricCurrentRange = 0x2AEF,
        ElectricCurrentSpecification = 0x2AF0,
        ElectricCurrentStatistics = 0x2AF1,
        Elevation = 0x2A6C,
        EmailAddress = 0x2A87,
        EmergencyID = 0x2B2D,
        EmergencyText = 0x2B2E,
        EncryptedDataKeyMaterial = 0x2B88,
        Energy = 0x2AF2,
        Energy32 = 0x2BA8,
        EnergyinaPeriodofDay = 0x2AF3,
        EnhancedBloodPressureMeasurement = 0x2B34,
        EnhancedIntermediateCuffPressure = 0x2B35,
        ESLAddress = 0x2BF6,
        ESLControlPoint = 0x2BFE,
        ESLCurrentAbsoluteTime = 0x2BF9,
        ESLDisplayInformation = 0x2BFA,
        ESLImageInformation = 0x2BFB,
        ESLLEDInformation = 0x2BFD,
        ESLResponseKeyMaterial = 0x2BF8,
        ESLSensorInformation = 0x2BFC,
        EstimatedServiceDate = 0x2BEF,
        EventStatistics = 0x2AF4,
        ExactTime256 = 0x2A0C,
        FatBurnHeartRateLowerLimit = 0x2A88,
        FatBurnHeartRateUpperLimit = 0x2A89,
        FirmwareRevisionString = 0x2A26,
        FirstName = 0x2A8A,
        FitnessMachineControlPoint = 0x2AD9,
        FitnessMachineFeature = 0x2ACC,
        FitnessMachineStatus = 0x2ADA,
        FiveZoneHeartRateLimits = 0x2A8B,
        FixedString16 = 0x2AF5,
        FixedString24 = 0x2AF6,
        FixedString36 = 0x2AF7,
        FixedString64 = 0x2BDE,
        FixedString8 = 0x2AF8,
        FloorNumber = 0x2AB2,
        FourZoneHeartRateLimits = 0x2B4C,
        GainSettingsAttribute = 0x2B78,
        Gender = 0x2A8C,
        GeneralActivityInstantaneousData = 0x2B3C,
        GeneralActivitySummaryData = 0x2B3D,
        GenericLevel = 0x2AF9,
        GlobalTradeItemNumber = 0x2AFA,
        GlucoseFeature = 0x2A51,
        GlucoseMeasurement = 0x2A18,
        GlucoseMeasurementContext = 0x2A34,
        GroupObjectType = 0x2BAC,
        GustFactor = 0x2A74,
        Handedness = 0x2B4A,
        HardwareRevisionString = 0x2A27,
        HearingAidFeatures = 0x2BDA,
        HearingAidPresetControlPoint = 0x2BDB,
        HeartRateControlPoint = 0x2A39,
        HeartRateMax = 0x2A8D,
        HeartRateMeasurement = 0x2A37,
        HeatIndex = 0x2A7A,
        Height = 0x2A8E,
        HIDControlPoint = 0x2A4C,
        HIDInformation = 0x2A4A,
        HighIntensityExerciseThreshold = 0x2B4D,
        HighResolutionHeight = 0x2B47,
        HighTemperature = 0x2BDF,
        HighVoltage = 0x2BE0,
        HipCircumference = 0x2A8F,
        HTTPControlPoint = 0x2ABA,
        HTTPEntityBody = 0x2AB9,
        HTTPHeaders = 0x2AB7,
        HTTPStatusCode = 0x2AB8,
        HTTPSSecurity = 0x2ABB,
        Humidity = 0x2A6F,
        IDDAnnunciationStatus = 0x2B22,
        IDDCommandControlPoint = 0x2B25,
        IDDCommandData = 0x2B26,
        IDDFeatures = 0x2B23,
        IDDHistoryData = 0x2B28,
        IDDRecordAccessControlPoint = 0x2B27,
        IDDStatus = 0x2B21,
        IDDStatusChanged = 0x2B20,
        IDDStatusReaderControlPoint = 0x2B24,
        IEEE11073_20601RegulatoryCertificationDataList = 0x2A2A,
        Illuminance = 0x2AFB,
        IncomingCall = 0x2BC1,
        IncomingCallTargetBearerURI = 0x2BBC,
        IndoorBikeData = 0x2AD2,
        IndoorPositioningConfiguration = 0x2AAD,
        IntermediateCuffPressure = 0x2A36,
        IntermediateTemperature = 0x2A1E,
        Irradiance = 0x2A77,
        Language = 0x2AA2,
        LastName = 0x2A90,
        Latitude = 0x2AAE,
        LEGATTSecurityLevels = 0x2BF5,
        LightDistribution = 0x2BE1,
        LightOutput = 0x2BE2,
        LightSourceType = 0x2BE3,
        LNControlPoint = 0x2A6B,
        LNFeature = 0x2A6A,
        LocalEastCoordinate = 0x2AB1,
        LocalNorthCoordinate = 0x2AB0,
        LocalTimeInformation = 0x2A0F,
        LocationandSpeed = 0x2A67,
        LocationName = 0x2AB5,
        Longitude = 0x2AAF,
        LuminousEfficacy = 0x2AFC,
        LuminousEnergy = 0x2AFD,
        LuminousExposure = 0x2AFE,
        LuminousFlux = 0x2AFF,
        LuminousFluxRange = 0x2B00,
        LuminousIntensity = 0x2B01,
        MagneticDeclination = 0x2A2C,
        MagneticFluxDensity_2D = 0x2AA0,
        MagneticFluxDensity_3D = 0x2AA1,
        ManufacturerNameString = 0x2A29,
        MassFlow = 0x2B02,
        MaximumRecommendedHeartRate = 0x2A91,
        MeasurementInterval = 0x2A21,
        MediaControlPoint = 0x2BA4,
        MediaControlPointOpcodesSupported = 0x2BA5,
        MediaPlayerIconObjectID = 0x2B94,
        MediaPlayerIconObjectType = 0x2BA9,
        MediaPlayerIconURL = 0x2B95,
        MediaPlayerName = 0x2B93,
        MediaState = 0x2BA3,
        MeshProvisioningDataIn = 0x2ADB,
        MeshProvisioningDataOut = 0x2ADC,
        MeshProxyDataIn = 0x2ADD,
        MeshProxyDataOut = 0x2ADE,
        MethaneConcentration = 0x2BD1,
        MiddleName = 0x2B48,
        ModelNumberString = 0x2A24,
        Mute = 0x2BC3,
        Navigation = 0x2A68,
        NewAlert = 0x2A46,
        NextTrackObjectID = 0x2B9E,
        NitrogenDioxideConcentration = 0x2BD2,
        Noise = 0x2BE4,
        Non_MethaneVolatileOrganicCompoundsConcentration = 0x2BD3,
        ObjectActionControlPoint = 0x2AC5,
        ObjectChanged = 0x2AC8,
        ObjectFirst_Created = 0x2AC1,
        ObjectID = 0x2AC3,
        ObjectLast_Modified = 0x2AC2,
        ObjectListControlPoint = 0x2AC6,
        ObjectListFilter = 0x2AC7,
        ObjectName = 0x2ABE,
        ObjectProperties = 0x2AC4,
        ObjectSize = 0x2AC0,
        ObjectType = 0x2ABF,
        OTSFeature = 0x2ABD,
        OzoneConcentration = 0x2BD4,
        ParentGroupObjectID = 0x2B9F,
        ParticulateMatter_PM1Concentration = 0x2BD5,
        ParticulateMatter_PM10Concentration = 0x2BD7,
        ParticulateMatter_PM2_5Concentration = 0x2BD6,
        PerceivedLightness = 0x2B03,
        Percentage8 = 0x2B04,
        PeripheralPreferredConnectionParameters = 0x2A04,
        PeripheralPrivacyFlag = 0x2A02,
        PhysicalActivityMonitorControlPoint = 0x2B43,
        PhysicalActivityMonitorFeatures = 0x2B3B,
        PhysicalActivitySessionDescriptor = 0x2B45,
        PlaybackSpeed = 0x2B9A,
        PlayingOrder = 0x2BA1,
        PlayingOrdersSupported = 0x2BA2,
        PLXContinuousMeasurement = 0x2A5F,
        PLXFeatures = 0x2A60,
        PLXSpot_CheckMeasurement = 0x2A5E,
        PnPID = 0x2A50,
        PollenConcentration = 0x2A75,
        PositionQuality = 0x2A69,
        Power = 0x2B05,
        PowerSpecification = 0x2B06,
        PreferredUnits = 0x2B46,
        Pressure = 0x2A6D,
        ProtocolMode = 0x2A4E,
        Rainfall = 0x2A78,
        RCFeature = 0x2B1D,
        RCSettings = 0x2B1E,
        ReconnectionAddress = 0x2A03,
        ReconnectionConfigurationControlPoint = 0x2B1F,
        RecordAccessControlPoint = 0x2A52,
        ReferenceTimeInformation = 0x2A14,
        RegisteredUser = 0x2B37,
        RelativeRuntimeinaCorrelatedColorTemperatureRange = 0x2BE5,
        RelativeRuntimeinaCurrentRange = 0x2B07,
        RelativeRuntimeinaGenericLevelRange = 0x2B08,
        RelativeValueinaPeriodofDay = 0x2B0B,
        RelativeValueinaTemperatureRange = 0x2B0C,
        RelativeValueinaVoltageRange = 0x2B09,
        RelativeValueinanIlluminanceRange = 0x2B0A,
        Report = 0x2A4D,
        ReportMap = 0x2A4B,
        ResolvablePrivateAddressOnly = 0x2AC9,
        RestingHeartRate = 0x2A92,
        RingerControlPoint = 0x2A40,
        RingerSetting = 0x2A41,
        RowerData = 0x2AD1,
        RSCFeature = 0x2A54,
        RSCMeasurement = 0x2A53,
        SCControlPoint = 0x2A55,
        ScanIntervalWindow = 0x2A4F,
        ScanRefresh = 0x2A31,
        SearchControlPoint = 0x2BA7,
        SearchResultsObjectID = 0x2BA6,
        SedentaryIntervalNotification = 0x2B4F,
        SeekingSpeed = 0x2B9B,
        SensorLocation = 0x2A5D,
        SerialNumberString = 0x2A25,
        ServerSupportedFeatures = 0x2B3A,
        ServiceChanged = 0x2A05,
        SetIdentityResolvingKey = 0x2B84,
        SetMemberLock = 0x2B86,
        SetMemberRank = 0x2B87,
        SinkASE = 0x2BC4,
        SinkAudioLocations = 0x2BCA,
        SinkPAC = 0x2BC9,
        SleepActivityInstantaneousData = 0x2B41,
        SleepActivitySummaryData = 0x2B42,
        SoftwareRevisionString = 0x2A28,
        SourceASE = 0x2BC5,
        SourceAudioLocations = 0x2BCC,
        SourcePAC = 0x2BCB,
        SportTypeforAerobicandAnaerobicThresholds = 0x2A93,
        StairClimberData = 0x2AD0,
        StatusFlags = 0x2BBB,
        StepClimberData = 0x2ACF,
        StepCounterActivitySummaryData = 0x2B40,
        StrideLength = 0x2B49,
        SulfurDioxideConcentration = 0x2BD8,
        SulfurHexafluorideConcentration = 0x2BD9,
        SupportedAudioContexts = 0x2BCE,
        SupportedHeartRateRange = 0x2AD7,
        SupportedInclinationRange = 0x2AD5,
        SupportedNewAlertCategory = 0x2A47,
        SupportedPowerRange = 0x2AD8,
        SupportedResistanceLevelRange = 0x2AD6,
        SupportedSpeedRange = 0x2AD4,
        SupportedUnreadAlertCategory = 0x2A48,
        SystemID = 0x2A23,
        TDSControlPoint = 0x2ABC,
        Temperature = 0x2A6E,
        Temperature8 = 0x2B0D,
        Temperature8inaPeriodofDay = 0x2B0E,
        Temperature8Statistics = 0x2B0F,
        TemperatureMeasurement = 0x2A1C,
        TemperatureRange = 0x2B10,
        TemperatureStatistics = 0x2B11,
        TemperatureType = 0x2A1D,
        TerminationReason = 0x2BC0,
        ThreeZoneHeartRateLimits = 0x2A94,
        TimeAccuracy = 0x2A12,
        TimeChangeLogData = 0x2B92,
        TimeDecihour8 = 0x2B12,
        TimeExponential8 = 0x2B13,
        TimeHour24 = 0x2B14,
        TimeMillisecond24 = 0x2B15,
        TimeSecond16 = 0x2B16,
        TimeSecond32 = 0x2BE6,
        TimeSecond8 = 0x2B17,
        TimeSource = 0x2A13,
        TimeUpdateControlPoint = 0x2A16,
        TimeUpdateState = 0x2A17,
        TimewithDST = 0x2A11,
        TimeZone = 0x2A0E,
        TMAPRole = 0x2B51,
        TrackChanged = 0x2B96,
        TrackDuration = 0x2B98,
        TrackObjectType = 0x2BAB,
        TrackPosition = 0x2B99,
        TrackSegmentsObjectType = 0x2BAA,
        TrackTitle = 0x2B97,
        TrainingStatus = 0x2AD3,
        TreadmillData = 0x2ACD,
        TrueWindDirection = 0x2A71,
        TrueWindSpeed = 0x2A70,
        TwoZoneHeartRateLimits = 0x2A95,
        TxPowerLevel = 0x2A07,
        Uncertainty = 0x2AB4,
        UnreadAlertStatus = 0x2A45,
        URI = 0x2AB6,
        UserControlPoint = 0x2A9F,
        UserIndex = 0x2A9A,
        UVIndex = 0x2A76,
        VO2Max = 0x2A96,
        VOCConcentration = 0x2BE7,
        Voltage = 0x2B18,
        VoltageFrequency = 0x2BE8,
        VoltageSpecification = 0x2B19,
        VoltageStatistics = 0x2B1A,
        VolumeControlPoint = 0x2B7E,
        VolumeFlags = 0x2B7F,
        VolumeFlow = 0x2B1B,
        VolumeOffsetControlPoint = 0x2B82,
        VolumeOffsetState = 0x2B80,
        VolumeState = 0x2B7D,
        WaistCircumference = 0x2A97,
        Weight = 0x2A98,
        WeightMeasurement = 0x2A9D,
        WeightScaleFeature = 0x2A9E,
        WindChill = 0x2A79,
        SimpleKeyState = 0xFFE1
    }

    /// <summary>
    ///     This enum assists in finding a string representation of a BT SIG assigned value for Descriptor UUIDs
    ///     Reference: https://developer.bluetooth.org/gatt/descriptors/Pages/DescriptorsHomePage.aspx
    /// </summary>
    public enum GattNativeDescriptorUuid : ushort
    {
        CharacteristicExtendedProperties = 0x2900,
        CharacteristicUserDescription = 0x2901,
        ClientCharacteristicConfiguration = 0x2902,
        ServerCharacteristicConfiguration = 0x2903,
        CharacteristicPresentationFormat = 0x2904,
        CharacteristicAggregateFormat = 0x2905,
        ValidRange = 0x2906,
        ExternalReportReference = 0x2907,
        ReportReference = 0x2908
    }

    public enum DataFormat
    {
        ASCII = 0,
        UTF8,
        Dec,
        Hex,
        Bin,
    }

    public enum ProtocolErrorCode : byte
    {
        Invalid_Handle = 0x01,
        Read_Not_Permitted = 0x02,
        Write_Not_Permitted = 0x03,
        Invalid_PDU = 0x04,
        Insufficient_Authentication = 0x05,
        Request_Not_Supported = 0x06,
        Invalid_Offset = 0x07,
        Insufficient_Authorization = 0x08,
        Prepare_Queue_Full = 0x09,
        Attribute_Not_Found = 0x0A,
        Attribute_Not_Long = 0x0B,
        Insufficient_Encryption_Key_Size = 0x0C,
        Invalid_Attribute_Value_Length = 0x0D,
        Unlikely_Error = 0x0E,
        Insufficient_Encryption = 0x0F,
        Unsupported_Group_Type = 0x10,
        Insufficient_Resource = 0x11,
        Database_Out_Of_Sync = 0x12,
        Value_Not_Allowed = 0x13,
        Write_Request_Rejected = 0xFC,
        Client_Characteristic_Configuration_Descriptor_Improperly_Configured = 0xFD,
        Procedure_Already_in_Progress = 0xFE,
        Out_of_Range = 0xFF
    }

    public static class Utilities
    {
        /// <summary>
        ///     Converts from standard 128bit UUID to the assigned 32bit UUIDs. Makes it easy to compare services
        ///     that devices expose to the standard list.
        /// </summary>
        /// <param name="uuid">UUID to convert to 32 bit</param>
        /// <returns></returns>
        public static ushort ConvertUuidToShortId(Guid uuid)
        {
            // Get the short Uuid
            var bytes = uuid.ToByteArray();
            var shortUuid = (ushort) (bytes[0] | (bytes[1] << 8));
            return shortUuid;
        }

        /// <summary>
        ///     Converts from a buffer to a properly sized byte array
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static byte[] ReadBufferToBytes(IBuffer buffer)
        {
            var dataLength = buffer.Length;
            var data = new byte[dataLength];
            using (var reader = DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(data);
            }
            return data;
        }

        /// <summary>
        /// This function converts IBuffer data to string by specified format
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static string FormatValue(IBuffer buffer, DataFormat format)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);

            switch (format)
            {
                case DataFormat.ASCII:
                    return Encoding.ASCII.GetString(data);

                case DataFormat.UTF8:
                    return Encoding.UTF8.GetString(data);

                case DataFormat.Dec:
                    return string.Join(" ", data.Select(b => b.ToString("00")));

                case DataFormat.Hex:
                    return BitConverter.ToString(data).Replace("-", " ");

                case DataFormat.Bin:
                    var s = string.Empty;
                    foreach (var b in data) s += Convert.ToString(b, 2).PadLeft(8, '0') + " ";
                    return s;

                default:
                    return Encoding.ASCII.GetString(data);
            }
        }

        /// <summary>
        /// Format data for writing by specific format
        /// </summary>
        /// <param name="data"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static IBuffer FormatData(string data, DataFormat format)
        {
            try
            {
                // For text formats, use CryptographicBuffer
                if (format == DataFormat.ASCII || format == DataFormat.UTF8)
                {
                    return CryptographicBuffer.ConvertStringToBinary(Regex.Unescape(data), BinaryStringEncoding.Utf8);
                }
                else
                {
                    string[] values = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    byte[] bytes = new byte[values.Length];

                    for (int i = 0; i < values.Length; i++)
                        bytes[i] = Convert.ToByte(values[i], (format == DataFormat.Dec ? 10 : (format == DataFormat.Hex ? 16 : 2)));

                    var writer = new DataWriter();
                    writer.ByteOrder = ByteOrder.LittleEndian;
                    writer.WriteBytes(bytes);

                    return writer.DetachBuffer();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(error.Message);
                return null;
            }
        }

        /// <summary>
        /// This function is trying to find device or service or attribute by name or number
        /// </summary>
        /// <param name="collection">source collection</param>
        /// <param name="name">name or number to find</param>
        /// <returns>ID for device, Name for services or attributes</returns>
        public static string GetIdByNameOrNumber (object collection, string name)
        {
            string result = string.Empty;

            // If number is specified, try to open BLE device by specific number
            if (name[0] == '#')
            {
                int devNumber = -1;
                if (int.TryParse(name.Substring(1), out devNumber))
                {
                    // Try to find device ID by number
                    if (collection is List<DeviceInformation>)
                    {
                        if (0 <= devNumber && devNumber < (collection as List<DeviceInformation>).Count)
                        {
                            result = (collection as List<DeviceInformation>)[devNumber].Id;
                        }
                        else
                            if(Console.IsOutputRedirected)
                                Console.WriteLine("Device number {0:00} is not in device list range", devNumber);
                    }
                    // for services or attributes
                    else
                    {
                        if (0 <= devNumber && devNumber < (collection as List<BluetoothLEAttributeDisplay>).Count)
                        {
                            result = (collection as List<BluetoothLEAttributeDisplay>)[devNumber].Name;
                        }
                    }
                }
                else
                    if (!Console.IsOutputRedirected)
                        Console.WriteLine("Invalid device number {0}", name.Substring(1));
            }
            // else try to find name
            else
            {
                // ... for devices
                if (collection is List<DeviceInformation>)
                {
                    var foundDevices = (collection as List<DeviceInformation>).Where(d => d.Name.ToLower().StartsWith(name.ToLower())).ToList();
                    if (foundDevices.Count == 0)
                    {
                        if (!Console.IsOutputRedirected)
                            Console.WriteLine("Can't connect to {0}.", name);
                    }
                    else if (foundDevices.Count == 1)
                    {
                        result = foundDevices.First().Id;
                    }
                    else
                    {
                        if (!Console.IsOutputRedirected)
                            Console.WriteLine("Found multiple devices with names started from {0}. Please provide an exact name.", name);
                    }
                }
                // for services or attributes
                else
                {
                    // search for service/characteristic by uuid
                    var foundByUuid = (collection as List<BluetoothLEAttributeDisplay>).Where(d => name.Equals(d.Uuid)).ToList();
                    if (foundByUuid.Count == 1)
                    {
                        return foundByUuid.First().Name;
                    }

                    // search for service/characteristic by name
                    var foundDispAttrs = (collection as List<BluetoothLEAttributeDisplay>).Where(d => d.Name.ToLower().StartsWith(name.ToLower())).ToList();
                    if (foundDispAttrs.Count == 0)
                    {
                        if(Console.IsOutputRedirected)
                            Console.WriteLine("No service/characteristic found by name {0}.", name);
                    }
                    else if (foundDispAttrs.Count == 1)
                    {
                        result = foundDispAttrs.First().Name;
                    }
                    else
                    {
                        if (Console.IsOutputRedirected)
                            Console.WriteLine("Found multiple services/characteristic with names started from {0}. Please provide an exact name.", name);
                    }
                }
            }
            return result;
        }

        public static string FormatProtocolError(byte? protocolError) 
        {
            if (protocolError == null)
            {
                return "";
            }
            string protocolErrorCodeName = Enum.GetName(typeof(ProtocolErrorCode), protocolError);
            if (protocolErrorCodeName != null)
            {
                protocolErrorCodeName = protocolErrorCodeName.Replace("_", " ");
                return String.Format("0x{0:X2}: {1}", protocolError, protocolErrorCodeName);
            }
            return String.Format("0x{0:X2}: Unknown", protocolError);
        }
    }

    public static class TaskExtensions
    {
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }
    }
}