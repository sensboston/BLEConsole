/*
 * BLE Notify Test Server for ESP32
 * Purpose: Testing BLEConsole subscription and pairing mechanisms
 * 
 * Service UUID: 12345678-1234-5678-1234-56789abcdef0
 * Characteristics:
 *   - Enabled (R/W):   12345678-1234-5678-1234-56789abcdef1 (uint8: 0=off, 1=on)
 *   - Interval (R/W):  12345678-1234-5678-1234-56789abcdef2 (uint16: ms, default 1000)
 *   - RandomVal (R/N): 12345678-1234-5678-1234-56789abcdef3 (uint8: 0-255, with notify)
 * 
 * Pairing modes (set PAIRING_MODE below):
 *   0 = No pairing (open access)
 *   1 = Static Passkey (fixed PIN, client enters it)
 *   2 = Display Passkey (ESP32 generates random PIN, shows in Serial)
 */

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

// ============== CONFIGURATION ==============

// Pairing mode: 0 = None, 1 = Static Passkey, 2 = Display Passkey
#define PAIRING_MODE 1

// Static passkey (used only if PAIRING_MODE == 1)
#define STATIC_PASSKEY 123456

// Clear bonding data on startup (set to 1 to reset, then back to 0)
#define CLEAR_BONDING 0

// ===========================================

#if PAIRING_MODE > 0 || CLEAR_BONDING
#include "esp_gap_ble_api.h"
#include "esp_gatt_defs.h"
#endif

// Built-in LED pin (GPIO2 for most ESP32 boards)
#define LED_PIN 2

// UUIDs
#define SERVICE_UUID        "12345678-1234-5678-1234-56789abcdef0"
#define CHAR_ENABLED_UUID   "12345678-1234-5678-1234-56789abcdef1"
#define CHAR_INTERVAL_UUID  "12345678-1234-5678-1234-56789abcdef2"
#define CHAR_RANDOM_UUID    "12345678-1234-5678-1234-56789abcdef3"

BLEServer* pServer = nullptr;
BLECharacteristic* pCharEnabled = nullptr;
BLECharacteristic* pCharInterval = nullptr;
BLECharacteristic* pCharRandom = nullptr;

bool deviceConnected = false;
bool oldDeviceConnected = false;

// Control variables
uint8_t enabled = 0;
uint16_t interval = 1000;
unsigned long lastUpdate = 0;

// Callback for server connection events
class ServerCallbacks : public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
        deviceConnected = true;
        Serial.println("Client connected");
    }

    void onDisconnect(BLEServer* pServer) {
        deviceConnected = false;
        Serial.println("Client disconnected");
    }
};

// Callback for Enabled characteristic write
class EnabledCallback : public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic* pCharacteristic) {
        uint8_t* data = pCharacteristic->getData();
        if (pCharacteristic->getLength() > 0) {
            enabled = data[0] ? 1 : 0;
            Serial.printf("Enabled set to: %d\n", enabled);
        }
    }
};

// Callback for Interval characteristic write
class IntervalCallback : public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic* pCharacteristic) {
        uint8_t* data = pCharacteristic->getData();
        size_t len = pCharacteristic->getLength();
        if (len >= 2) {
            // Little-endian
            interval = data[0] | (data[1] << 8);
        } else if (len == 1) {
            interval = data[0];
        }
        // Minimum 100ms to prevent flooding
        if (interval < 100) interval = 100;
        Serial.printf("Interval set to: %d ms\n", interval);
    }
};

#if PAIRING_MODE > 0
// Callback for security/pairing events
class SecurityCallbacks : public BLESecurityCallbacks {
    uint32_t onPassKeyRequest() {
        // Called when client requests passkey (Static Passkey mode)
        Serial.println("PassKey requested by client");
#if PAIRING_MODE == 1
        Serial.printf("Returning static passkey: %06d\n", STATIC_PASSKEY);
        return STATIC_PASSKEY;
#else
        return 0;
#endif
    }

    void onPassKeyNotify(uint32_t pass_key) {
        // Called when ESP32 generates passkey to display (Display Passkey mode)
        Serial.println("========================================");
        Serial.printf("  ENTER THIS PASSKEY: %06d\n", pass_key);
        Serial.println("========================================");
    }

    bool onConfirmPIN(uint32_t pass_key) {
        // Numeric comparison (not used in our modes)
        Serial.printf("Confirm PIN: %06d\n", pass_key);
        return true;
    }

    bool onSecurityRequest() {
        Serial.println("Security request received");
        return true;
    }

    void onAuthenticationComplete(esp_ble_auth_cmpl_t auth_cmpl) {
        if (auth_cmpl.success) {
            Serial.println("*** Pairing successful! ***");
            Serial.printf("    Auth mode: %d (0=JustWorks, 1=PasskeyEntry)\n", auth_cmpl.auth_mode);
        } else {
            Serial.printf("*** Pairing FAILED, reason: 0x%x ***\n", auth_cmpl.fail_reason);
        }
    }
};
#endif // PAIRING_MODE > 0

void setup() {
    Serial.begin(115200);
    Serial.println("\n========================================");
    Serial.println("BLE Notify Test Server");
    Serial.println("========================================");

    // Initialize LED
    pinMode(LED_PIN, OUTPUT);
    digitalWrite(LED_PIN, LOW);

    // Initialize BLE
    BLEDevice::init("ESP32_NotifyTest");

#if CLEAR_BONDING
    // Erase BLE bonding data only (must be after BLEDevice::init)
    Serial.println("Clearing BLE bonding data...");
    int dev_num = esp_ble_get_bond_device_num();
    if (dev_num > 0) {
        esp_ble_bond_dev_t *dev_list = (esp_ble_bond_dev_t *)malloc(sizeof(esp_ble_bond_dev_t) * dev_num);
        esp_ble_get_bond_device_list(&dev_num, dev_list);
        for (int i = 0; i < dev_num; i++) {
            esp_ble_remove_bond_device(dev_list[i].bd_addr);
        }
        free(dev_list);
        Serial.printf("Removed %d bonded device(s)\n", dev_num);
    } else {
        Serial.println("No bonded devices to clear");
    }
#endif

#if PAIRING_MODE == 0
    // No pairing
    Serial.println("Pairing mode: DISABLED (open access)");
    
#elif PAIRING_MODE == 1
    // Static Passkey mode
    Serial.println("Pairing mode: STATIC PASSKEY");
    Serial.printf("Passkey: %06d\n", STATIC_PASSKEY);
    
    BLEDevice::setSecurityCallbacks(new SecurityCallbacks());
    
    // Set static passkey
    uint32_t passkey = STATIC_PASSKEY;
    esp_ble_gap_set_security_param(ESP_BLE_SM_SET_STATIC_PASSKEY, &passkey, sizeof(uint32_t));
    
    // IO capability: Display Only (ESP32 "displays" the static passkey, client enters it)
    esp_ble_io_cap_t iocap = ESP_IO_CAP_OUT;
    esp_ble_gap_set_security_param(ESP_BLE_SM_IOCAP_MODE, &iocap, sizeof(esp_ble_io_cap_t));
    
    // Auth mode: bonding + MITM
    esp_ble_auth_req_t auth_req = ESP_LE_AUTH_REQ_SC_MITM_BOND;
    esp_ble_gap_set_security_param(ESP_BLE_SM_AUTHEN_REQ_MODE, &auth_req, sizeof(esp_ble_auth_req_t));
    
    // Max encryption key size
    uint8_t key_size = 16;
    esp_ble_gap_set_security_param(ESP_BLE_SM_MAX_KEY_SIZE, &key_size, sizeof(uint8_t));
    
#elif PAIRING_MODE == 2
    // Display Passkey mode
    Serial.println("Pairing mode: DISPLAY PASSKEY");
    Serial.println("(Random passkey will be shown when client connects)");
    
    BLEDevice::setSecurityCallbacks(new SecurityCallbacks());
    
    // IO capability: Display Only (we display generated passkey)
    esp_ble_io_cap_t iocap = ESP_IO_CAP_OUT;
    esp_ble_gap_set_security_param(ESP_BLE_SM_IOCAP_MODE, &iocap, sizeof(esp_ble_io_cap_t));
    
    // Auth mode: bonding + MITM
    esp_ble_auth_req_t auth_req = ESP_LE_AUTH_REQ_SC_MITM_BOND;
    esp_ble_gap_set_security_param(ESP_BLE_SM_AUTHEN_REQ_MODE, &auth_req, sizeof(esp_ble_auth_req_t));
    
    // Max encryption key size
    uint8_t key_size = 16;
    esp_ble_gap_set_security_param(ESP_BLE_SM_MAX_KEY_SIZE, &key_size, sizeof(uint8_t));
    
#endif

    Serial.println("----------------------------------------");

    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new ServerCallbacks());

    // Create service
    BLEService* pService = pServer->createService(SERVICE_UUID);

    // Create Enabled characteristic (Read + Write)
    pCharEnabled = pService->createCharacteristic(
        CHAR_ENABLED_UUID,
        BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_WRITE
    );
#if PAIRING_MODE > 0
    pCharEnabled->setAccessPermissions(ESP_GATT_PERM_READ_ENCRYPTED | ESP_GATT_PERM_WRITE_ENCRYPTED);
#endif
    pCharEnabled->setCallbacks(new EnabledCallback());
    pCharEnabled->setValue(&enabled, 1);

    // Create Interval characteristic (Read + Write)
    pCharInterval = pService->createCharacteristic(
        CHAR_INTERVAL_UUID,
        BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_WRITE
    );
#if PAIRING_MODE > 0
    pCharInterval->setAccessPermissions(ESP_GATT_PERM_READ_ENCRYPTED | ESP_GATT_PERM_WRITE_ENCRYPTED);
#endif
    pCharInterval->setCallbacks(new IntervalCallback());
    uint8_t intervalBytes[2] = { (uint8_t)(interval & 0xFF), (uint8_t)(interval >> 8) };
    pCharInterval->setValue(intervalBytes, 2);

    // Create Random characteristic (Read + Notify)
    pCharRandom = pService->createCharacteristic(
        CHAR_RANDOM_UUID,
        BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_NOTIFY
    );
#if PAIRING_MODE > 0
    pCharRandom->setAccessPermissions(ESP_GATT_PERM_READ_ENCRYPTED);
#endif
    // Add CCCD descriptor for notify (required for subscriptions)
    pCharRandom->addDescriptor(new BLE2902());
    uint8_t initRandom = 0;
    pCharRandom->setValue(&initRandom, 1);

    // Start service
    pService->start();

    // Start advertising
    BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(true);
    pAdvertising->setMinPreferred(0x06);
    pAdvertising->setMinPreferred(0x12);
    BLEDevice::startAdvertising();

    Serial.println("BLE server ready. Device name: ESP32_NotifyTest");
    Serial.println("Waiting for connection...");
}

void loop() {
    // Handle reconnection
    if (!deviceConnected && oldDeviceConnected) {
        delay(500);
        pServer->startAdvertising();
        Serial.println("Restarted advertising");
        oldDeviceConnected = deviceConnected;
    }

    if (deviceConnected && !oldDeviceConnected) {
        oldDeviceConnected = deviceConnected;
    }

    // Send notifications if enabled and connected
    if (deviceConnected && enabled) {
        unsigned long now = millis();
        if (now - lastUpdate >= interval) {
            lastUpdate = now;

            // Generate random value
            uint8_t randomVal = random(0, 256);
            pCharRandom->setValue(&randomVal, 1);
            pCharRandom->notify();

            // Blink LED
            digitalWrite(LED_PIN, HIGH);
            
            Serial.printf("Notify sent: %d\n", randomVal);
            
            // Short LED pulse (non-blocking would be better but keeping it simple)
            delay(50);
            digitalWrite(LED_PIN, LOW);
        }
    }

    delay(10);
}
