/*
*This is auto generated from the ControlManifest.Input.xml file
*/

// Define IInputs and IOutputs Type. They should match with ControlManifest.
export interface IInputs {
    BusinessIdentifier:ComponentFramework.PropertyTypes.StringProperty;
    BannerMessage: ComponentFramework.PropertyTypes.StringProperty;
    BannerStatus: ComponentFramework.PropertyTypes.TwoOptionsProperty;

}
export interface IOutputs {
    sampleProperty?: string;
}
