# Location Services Setup Guide

## ? **Location Services Configuration Complete**

### **Configuration Files Updated:**

? `Platforms/Android/AndroidManifest.xml` - Added location permissions  
? `Platforms/iOS/Info.plist` - Added location descriptions  
? `Platforms/MacCatalyst/Info.plist` - Added location descriptions  

---

## ?? **Enable Location Services by Platform**

### **Windows 10/11 (Desktop)**

#### Step 1: Enable System Location
1. Press **Win + I** to open Settings
2. Go to **Privacy & Security** ? **Location**
3. Toggle **Location** to **ON**
4. Ensure **Allow apps to access your location** is enabled

#### Step 2: Grant App Permission
1. Scroll down in the Location settings
2. Find **Tracking Map App** in the list
3. Toggle to **Allow**

#### Step 3: Run the App
- The app will request location permission on first run
- Click **Yes** when prompted

---

### **Android**

#### Method 1: System Settings (Recommended)

1. **Enable Location Services**
   - Open **Settings** ? **Location**
   - Toggle **Location** to **ON**
   - Select **Accuracy**: "High accuracy" (uses GPS + Network)

2. **Grant App Permissions**
   - Go to **Settings** ? **Apps** (or **Application Manager**)
   - Search for **Tracking Map App**
   - Tap **Permissions**
   - Enable:
     - ? **Location**
     - ? **Approximate location** (if available)
     - ? **Precise location** (if available)

3. **Allow Background Location (Optional)**
   - For continuous tracking even when app is minimized:
   - Settings ? Apps ? Tracking Map App ? Permissions ? Location
   - Select **Allow all the time** (instead of "While using the app")

#### Method 2: During App Use

1. Open the app
2. Click **"Start Tracking"**
3. When prompted, tap **Allow** for location permission
4. The app will automatically start capturing location data

#### Manifest Permissions Configured:
```xml
<!-- Precise GPS location -->
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />

<!-- Network-based location -->
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />

<!-- Background location tracking -->
<uses-permission android:name="android.permission.ACCESS_BACKGROUND_LOCATION" />
```

---

### **iOS (iPhone/iPad)**

#### Step 1: Enable System Location
1. Open **Settings** ? **Privacy & Security** ? **Location Services**
2. Toggle **Location Services** to **ON**

#### Step 2: Configure App Permission
1. In the same **Location Services** menu
2. Scroll down and find **Tracking Map App**
3. Select permission level:
   - **Never** - No location access
   - **While Using the App** - Access only when app is open
   - **Always** - Continuous background tracking

#### Step 3: Run the App
1. Open the app
2. Click **"Start Tracking"**
3. A permission dialog will appear
4. Tap **Allow Once** or **Allow While Using App** or **Always Allow**

#### Configured Descriptions (Shown in Permission Dialog):
- **NSLocationWhenInUseUsageDescription**: "This app needs access to your location to track your movements and display them on a heat map."
- **NSLocationAlwaysAndWhenInUseUsageDescription**: "This app needs access to your location all the time to track your movements and display them on a heat map."

---

### **macOS (Mac Catalyst)**

#### Step 1: Grant App Permission
1. Open the app
2. Click **"Start Tracking"**
3. macOS will prompt for location permission
4. Click **Allow**

#### Step 2: System Settings (if needed)
1. Go to **System Settings** ? **Privacy & Security** ? **Location Services**
2. Ensure **Location Services** is enabled
3. Find **Tracking Map App** and ensure it's allowed

---

## ?? **How the App Uses Location**

### **Location Tracking Flow:**

```
1. User taps "Start Tracking"
   ?
2. App checks location permissions
   ?
3. If not granted, prompts user for permission
   ?
4. Once granted, begins capturing location every 5 seconds
   ?
5. Each location is saved to local database (JSON file)
   ?
6. Heat map updates in real-time showing location density
   ?
7. User can stop tracking anytime
```

### **Data Captured:**
- **Latitude** - Geographic north/south position
- **Longitude** - Geographic east/west position
- **Timestamp** - Date and time of capture
- **Accuracy** - GPS accuracy in meters
- **Altitude** - Height above sea level
- **Speed** - Movement speed in m/s

---

## ??? **Using the Heat Map**

### **Color Intensity Legend:**

| Color | Intensity | Meaning |
|-------|-----------|---------|
| ?? Blue | 0-25% | Low concentration |
| ?? Green | 25-50% | Medium concentration |
| ?? Yellow | 50-75% | High concentration |
| ?? Red | 75-100%+ | Very high concentration |

### **Tips for Best Results:**

1. **Use "High Accuracy" Mode** - On Android, select "High accuracy" for GPS + Network location
2. **Keep App Open** - For best results, keep the app open while tracking
3. **Enable Background Location** - For continuous tracking even when minimized
4. **Multiple Sessions** - Run multiple tracking sessions to build a comprehensive heat map
5. **Clear Data** - Use "Clear Data" button to start fresh

---

## ?? **Troubleshooting**

### **"Location permission denied"**
- ? Check that location services are enabled on your device
- ? Go to app settings and grant location permission
- ? Restart the app

### **Heat map not updating**
- ? Ensure "Start Tracking" is active (button shows "Stop Tracking")
- ? Wait at least 10 seconds (2 location updates)
- ? Move around to generate multiple location points

### **No GPS signal**
- ? Make sure you're outdoors with clear sky view
- ? Wait 30-60 seconds for GPS to acquire signal
- ? On Android, switch to "High accuracy" mode

### **Background tracking not working**
- ? Android: Grant "Allow all the time" permission
- ? iOS: Select "Always Allow" permission
- ? Check device battery saver settings

---

## ?? **Data Storage**

Location data is stored locally on your device:

- **Windows**: `%APPDATA%/Tracking_Map_App/locations.json`
- **Android**: App's private storage directory
- **iOS**: App sandbox directory
- **macOS**: App sandbox directory

**No data is sent to servers.** All tracking is completely local and private.

---

## ?? **Privacy & Security**

- ? **All location data stays on your device**
- ? **No cloud sync or uploads**
- ? **No tracking of personal information**
- ? **Data can be deleted anytime with "Clear Data" button**
- ? **Fully compliant with iOS and Android privacy guidelines**

---

## ? **You're All Set!**

Your Location Heat Map Tracking App is now fully configured with location services enabled. Start tracking your movements and visualize them as a heat map!

**Happy tracking! ?????**
