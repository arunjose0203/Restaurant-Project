const {withDangerousMod,withMainApplication,withAndroidManifest}=require('@expo/config-plugins');
const fs=require('fs');const path=require('path');
module.exports=config=>{
 config=withAndroidManifest(config,mod=>{const manifest=mod.modResults.manifest;manifest['uses-permission']||=[];for(const name of ['android.permission.BLUETOOTH','android.permission.BLUETOOTH_CONNECT'])if(!manifest['uses-permission'].some(p=>p.$['android:name']===name))manifest['uses-permission'].push({$:{'android:name':name}});return mod;});
 config=withMainApplication(config,mod=>{if(!mod.modResults.contents.includes('ThermalPrinterPackage()'))mod.modResults.contents=mod.modResults.contents.replace('PackageList(this).packages.apply {','PackageList(this).packages.apply {\n              add(ThermalPrinterPackage())');return mod;});
 return withDangerousMod(config,['android',async mod=>{const pkg=config.android.package;const dir=path.join(mod.modRequest.platformProjectRoot,'app/src/main/java',...pkg.split('.'));fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(path.join(dir,'ThermalPrinterPackage.kt'),`package ${pkg}
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothManager
import android.content.Context
import android.util.Base64
import com.facebook.react.ReactPackage
import com.facebook.react.bridge.*
import com.facebook.react.uimanager.ViewManager
import java.util.UUID
import java.util.concurrent.Executors

class ThermalPrinterPackage: ReactPackage {
 override fun createNativeModules(context: ReactApplicationContext): List<NativeModule> = listOf(ThermalPrinterModule(context))
 override fun createViewManagers(context: ReactApplicationContext): List<ViewManager<*, *>> = emptyList()
}
class ThermalPrinterModule(private val context: ReactApplicationContext): ReactContextBaseJavaModule(context) {
 private val executor = Executors.newSingleThreadExecutor()
 override fun getName() = "ThermalPrinter"
 private fun adapter(): BluetoothAdapter = (context.getSystemService(Context.BLUETOOTH_SERVICE) as BluetoothManager).adapter ?: throw IllegalStateException("Bluetooth unavailable")
 @ReactMethod fun pairedDevices(promise: Promise) {
  try { val devices = Arguments.createArray(); for (device in adapter().bondedDevices) { val row=Arguments.createMap();row.putString("name",device.name ?: device.address);row.putString("address",device.address);devices.pushMap(row) };promise.resolve(devices) } catch(e: Exception){promise.reject("BLUETOOTH",e.message,e)}
 }
 @ReactMethod fun printBluetooth(address: String, data: String, promise: Promise) {
  executor.execute { try {
   val bytes=Base64.decode(data,Base64.DEFAULT);if(bytes.size>65536)throw IllegalArgumentException("Receipt too large")
   val device=adapter().bondedDevices.firstOrNull{it.address==address} ?: throw IllegalArgumentException("Pair this printer in Android settings first")
   val socket=device.createRfcommSocketToServiceRecord(UUID.fromString("00001101-0000-1000-8000-00805F9B34FB"))
   val timer=java.util.Timer();timer.schedule(object:java.util.TimerTask(){override fun run(){try{socket.close()}catch(_:Exception){}}},15000)
   try{socket.connect();socket.outputStream.write(bytes);socket.outputStream.flush();promise.resolve("Sent to printer")}finally{timer.cancel();socket.close()}
  }catch(e:Exception){promise.reject("PRINT",e.message,e)} }
 }
 override fun invalidate(){executor.shutdownNow();super.invalidate()}
}
`);return mod;}]);
};
