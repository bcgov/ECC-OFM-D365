import { EnvelopeCompositeControl, IEnvelopeField, IEnvelopeProps } from "./EnvelopeComposite";
import { IInputs, IOutputs } from "./generated/ManifestTypes";
import * as React from "react";
/* eslint no-unused-vars : "off" */

function isIEnvelopeField(field : IEnvelopeField | null ): field is IEnvelopeField {
	return field != null
  }

export class FundingEnvelopeControl implements ComponentFramework.ReactControl<IInputs, IOutputs> {
    private notifyOutputChanged: () => void;
    private newValue : Object =  {};

    /**
     * Empty constructor.
     */
    constructor() { }

    /**
     * Used to initialize the control instance. Controls can kick off remote server calls and other initialization actions here.
     * Data-set values are not initialized here, use updateView.
     * @param context The entire property bag available to control via Context Object; It contains values as set up by the customizer mapped to property names defined in the manifest, as well as utility functions.
     * @param notifyOutputChanged A callback method to alert the framework that the control has new outputs ready to be retrieved asynchronously.
     * @param state A piece of data that persists in one session for a single user. Can be set at any point in a controls life cycle by calling 'setControlState' in the Mode interface.
     */
    public init(
        context: ComponentFramework.Context<IInputs>,
        notifyOutputChanged: () => void,
        state: ComponentFramework.Dictionary
        ): void {

        this.notifyOutputChanged = notifyOutputChanged;  
		this.renderControl(context);
    }

    /**
     * Called when any value in the property bag has changed. This includes field values, data-sets, global values such as container height and width, offline status, control metadata values such as label, visible, etc.
     * @param context The entire property bag available to control via Context Object; It contains values as set up by the customizer mapped to names defined in the manifest, as well as utility functions
     * @returns ReactElement root react element for the control
     */
    public updateView(context: ComponentFramework.Context<IInputs>): React.ReactElement {
        // console.log("index - updateView(): " + JSON.stringify({"this.newvalue": this.newValue},null,2));
		return this.renderControl(context);
    }

    /**
     * It is called by the framework prior to a control receiving new data.
     * @returns an object based on nomenclature defined in manifest, expecting object[s] for property marked as “bound” or “output”
     */
    public getOutputs(): IOutputs {
        // console.log("index - getOutputs(): " + JSON.stringify({"this.newvalue": this.newValue},null,2))
        return this.newValue;       
    }

    /**
     * Called when the control is to be removed from the DOM tree. Controls should use this call for cleanup.
     * i.e. cancelling any pending remote calls, removing listeners, etc.
     */
    public destroy(): void {
        // Add code to cleanup control if necessary
        // console.log("index - destroy(): " + JSON.stringify({"this.newvalue": this.newValue},null,2));
    }

    private onChangeAmount = (newValue: Object) : void => {
		this.newValue =  Object.assign(this.newValue, newValue);
		this.notifyOutputChanged();
	}

    private renderControl(context: ComponentFramework.Context<IInputs>) : React.ReactElement  {
        let isReadOnly = context.mode.isControlDisabled || (<any> context).page.isPageReadOnly;
        let isMasked = context.mode.isVisible;

        if(context.parameters.field0.security){
            isReadOnly = isReadOnly || !context.parameters.field0.security.editable;
            isMasked = !context.parameters.field0.security.readable;
        }

        const paramNames = Array(50).fill(null);

        let fields : IEnvelopeField[] = paramNames.map((name, index) => {
            const ctrlName = `field${index + 1}`			
            if((context.parameters as any)[ctrlName]?.type == null){
                return null;
            }
            return {
                control:(context.parameters as any)[ctrlName] as ComponentFramework.PropertyTypes.NumberProperty, 
                name : ctrlName
            }
        }).filter(isIEnvelopeField);

        // this.newValue = fields.reduce((result, current) => {      
        //         return Object.assign(result, {[current.name]: current.control.raw!});
        // }, {});
        
		let params : IEnvelopeProps = {		
            pcfContext: context,	
			fields: fields,
			onValueChanged : this.onChangeAmount, 
			isReadOnly : isReadOnly,
			isMasked : isMasked, 
		};

		return React.createElement(EnvelopeCompositeControl, params);
	}  
}