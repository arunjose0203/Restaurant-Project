const {withAndroidManifest, withAppBuildGradle, withDangerousMod} = require('expo/config-plugins');
const fs = require('node:fs');
const path = require('node:path');

module.exports = function withReleaseAndroid(config) {
  config = withAndroidManifest(config, config => {
    const app = config.modResults.manifest.application[0].$;
    app['android:icon'] = '@drawable/tableflow_icon';
    app['android:roundIcon'] = '@drawable/tableflow_icon';
    app['android:allowBackup'] = 'false';
    app['android:usesCleartextTraffic'] = 'false';
    return config;
  });
  config = withDangerousMod(config, ['android', async config => {
    const directory = path.join(config.modRequest.platformProjectRoot, 'app/src/main/res/drawable');
    fs.mkdirSync(directory, {recursive: true});
    fs.writeFileSync(path.join(directory, 'tableflow_icon.xml'), `<vector xmlns:android="http://schemas.android.com/apk/res/android" android:width="108dp" android:height="108dp" android:viewportWidth="108" android:viewportHeight="108">
  <path android:fillColor="#165B45" android:pathData="M0,0 H108 V108 H0 Z"/>
  <path android:fillColor="#FFFFFF" android:pathData="M43,27 H54 V40 H69 V50 H54 V68 Q54,76 63,76 H69 V86 H60 Q43,86 43,69 V50 H33 V40 H43 Z"/>
  <path android:fillColor="#C4DC9C" android:pathData="M76,75 H87 V86 H76 Z"/>
</vector>`);
    return config;
  }]);
  return withAppBuildGradle(config, config => {
    const marker = '// Tableflow private release signing';
    const existing = config.modResults.contents.indexOf(marker);
    if (existing !== -1) config.modResults.contents = config.modResults.contents.slice(0, existing);
    config.modResults.contents += `
${marker}
def tableflowSigningFile = rootProject.file('../signing/release.properties')
def tableflowSigning = new Properties()
if (tableflowSigningFile.exists()) {
    tableflowSigningFile.withInputStream { tableflowSigning.load(it) }
}
android {
    signingConfigs {
        release {
            if (tableflowSigningFile.exists()) {
                storeFile rootProject.file('../signing/tableflow-release.p12')
                storePassword tableflowSigning['storePassword']
                keyAlias tableflowSigning['keyAlias']
                keyPassword tableflowSigning['keyPassword']
            }
        }
    }
    buildTypes { release { signingConfig signingConfigs.release } }
}
gradle.taskGraph.whenReady { graph ->
    if (!tableflowSigningFile.exists() && graph.allTasks.any { it.name.toLowerCase().contains('release') }) {
        throw new GradleException('Release signing is missing. Run scripts/build-android.ps1 to create a private signing key.')
    }
}
`;
    return config;
  });
};
