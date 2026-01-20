# Troubleshooting Guide

Common issues and their solutions when using the LicenseSeat Unity SDK.

## Installation Issues

### Package Not Found / Could Not Resolve

**Symptoms:**
- "Could not resolve package" error
- Package doesn't appear in Package Manager

**Solutions:**
1. Ensure Git is installed and in your system PATH
2. Check internet connectivity
3. Verify the repository URL is correct
4. Try closing and reopening Unity
5. Delete `Library/PackageCache` and restart Unity

### Compilation Errors After Install

**Symptoms:**
- Red errors in Console after package import
- Scripts fail to compile

**Solutions:**
1. Check Unity version (requires 2021.3+)
2. Set API Compatibility Level:
   - Edit > Project Settings > Player > Other Settings
   - Set "Api Compatibility Level" to ".NET Standard 2.0" or ".NET 4.x"
3. Try Assets > Reimport All

## Runtime Issues

### "LicenseSeatManager not found"

**Symptoms:**
- NullReferenceException when accessing manager
- "Manager not found" errors

**Solutions:**
1. Ensure LicenseSeatManager is in your scene
2. Check it's not being destroyed on scene load
3. If using DontDestroyOnLoad, ensure only one instance exists

### License Activation Fails

**Symptoms:**
- Activation returns error
- "Invalid license key" message

**Solutions:**
1. Verify API Key is correct in Settings
2. Verify Product ID matches your LicenseSeat dashboard
3. Check the license key format
4. Enable Debug Logging to see detailed error messages
5. Check network connectivity

### Validation Always Fails

**Symptoms:**
- ValidateAsync returns invalid
- ValidationFailed event fires repeatedly

**Solutions:**
1. Ensure the license was properly activated first
2. Check license hasn't expired
3. Verify the machine fingerprint matches (if machine-locked)
4. Check API key permissions

### Network Errors

**Symptoms:**
- "Connection refused" errors
- Timeout errors

**Solutions:**
1. Check internet connectivity
2. Verify Base URL is correct
3. Check firewall settings
4. Enable offline fallback mode for graceful degradation

## Platform-Specific Issues

### WebGL Build Fails

**Symptoms:**
- Build errors related to System.Net
- Runtime errors in browser

**Solutions:**
1. The SDK automatically uses UnityWebRequest on WebGL
2. Ensure your API server has proper CORS headers:
   ```
   Access-Control-Allow-Origin: *
   Access-Control-Allow-Methods: GET, POST, PUT, DELETE
   Access-Control-Allow-Headers: Content-Type, Authorization
   ```
3. Use HTTPS for your API endpoint

### IL2CPP Build Crashes

**Symptoms:**
- Build succeeds but app crashes at runtime
- "ExecutionEngineException" errors

**Solutions:**
1. Verify `link.xml` is included (should be in package)
2. Check Player Settings > Other Settings > "Managed Stripping Level" is not "High"
3. Try "Minimal" stripping level
4. If using custom types, add them to link.xml

### iOS/Android Specific

**Symptoms:**
- Works in Editor but not on device
- "DllNotFoundException" (shouldn't happen with pure C#)

**Solutions:**
1. Clean build (delete Builds folder)
2. Check iOS/Android specific console logs
3. Ensure all required permissions are granted
4. Verify network security config allows your API domain

## Editor Issues

### Settings Window Doesn't Open

**Symptoms:**
- Window > LicenseSeat > Settings does nothing
- Menu item missing

**Solutions:**
1. Check Console for compilation errors
2. Reimport the package
3. Restart Unity

### Settings Not Saving

**Symptoms:**
- Changes to Settings asset don't persist
- Values reset on play

**Solutions:**
1. Ensure Settings asset is saved (Ctrl+S)
2. Check if asset is read-only
3. Don't modify settings during runtime

## Performance Issues

### Frame Drops During Validation

**Symptoms:**
- Game stutters when validating
- GC spikes in Profiler

**Solutions:**
1. Use coroutines instead of blocking calls
2. Don't validate every frame
3. Use longer auto-validate intervals (300+ seconds)
4. Cache validation results

### Memory Leaks

**Symptoms:**
- Memory grows over time
- GC never collects certain objects

**Solutions:**
1. Ensure you unsubscribe from events in OnDestroy
2. Use the coroutine API (manages task lifetime)
3. Cancel async operations when objects are destroyed

## Debug Mode

Enable detailed logging to diagnose issues:

1. Select your LicenseSeatSettings asset
2. Check "Enable Debug Logging"
3. Open Console window (Window > General > Console)
4. Look for `[LicenseSeat]` prefixed messages

## Getting Help

If you can't resolve your issue:

1. **Check Documentation**: https://docs.licenseseat.com/sdk/unity
2. **Search Issues**: https://github.com/licenseseat/licenseseat-csharp/issues
3. **Create Issue**: Include:
   - Unity version
   - Target platform
   - Scripting backend (Mono/IL2CPP)
   - Full error message
   - Steps to reproduce
4. **Contact Support**: support@licenseseat.com
