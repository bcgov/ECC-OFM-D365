import * as React from 'react';
import { IStackStyles, Label, Stack, TextField } from '@fluentui/react';
import { EnvelopeField } from './EnvelopeField';

const stackTokens = { childrenGap: 25 };

export interface IEnvelopeField{
    control : ComponentFramework.PropertyTypes.NumberProperty;
    name : string;
};

export interface IEnvelopeProps{
    fields : IEnvelopeField[];
    isReadOnly: boolean;
    isMasked : boolean;
    inflation: number;
    onValueChanged : (newValue:Object) => void;
};

export const EnvelopeCompositeControl = React.memo(
    function EnvelopeCompositeControlApp({fields, isReadOnly, isMasked, inflation, onValueChanged}: IEnvelopeProps) 
    : JSX.Element{

    let fieldIndex = 0;
    const dataHR = [
        { column01: "Wages & Paid Time Off", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Benefits", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Employer Health Inflation Rate", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },    
        { column01: "Professional Development Hours", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }, 
        { column01: "Professional Development Expenses", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] }
    ];

    const dataNonHR = [
        { column01: "Programming", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Administrative", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++] },
        { column01: "Operational", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++]},
        { column01: "Facility", column02: fields[fieldIndex++], column03: fields[fieldIndex++], column04: fields[fieldIndex++]}
    ];

    let HRTotal_Colum02 = dataHR.reduce((a,v) =>  a = a + v.column02.control.raw! , 0 );
    let NonHRTotal_Colum02 = (dataNonHR.reduce((a,v) =>  a = a + v.column02.control.raw! , 0 ));
    let HRTotal_Colum03 = dataHR.reduce((a,v) =>  a = a + v.column03.control.raw! , 0 );
    let NonHRTotal_Colum03 = (dataNonHR.reduce((a,v) =>  a = a + v.column03.control.raw! , 0 ));
    let HRTotal_Colum04 = dataHR.reduce((a,v) =>  a = a + v.column04.control.raw! , 0 );
    let NonHRTotal_Colum04 = (dataNonHR.reduce((a,v) =>  a = a + v.column04.control.raw! , 0 ));

    console.log(JSON.stringify({
        "The column02 HR Total" : HRTotal_Colum02,
        "The column02 Non-HR Total" : NonHRTotal_Colum02,
        "The column03 HR Total" : HRTotal_Colum03,
        "The column03 Non-HR Total" : + NonHRTotal_Colum03,
        "The column04 HR Total" : HRTotal_Colum04,
        "The column04 Non-HR Total" : + NonHRTotal_Colum04,
        "MaxValue": dataNonHR[0].column03.control.attributes?.MaxValue
    }, null, 2));
    
    const infText =" (plus % inflation as defined by ministry)";
    
    return (
      <Stack tokens={stackTokens} >
        <Stack style={{width:"100%"}} >   
            <table style={{width:"100%"}} >
              <thead>
                {/* <tr>
                    <th colSpan={4} style={{textAlign: "left"}}>Annual Base Funding</th>              
                </tr> */}
                </thead>
                <tbody>
                <tr style={{textAlign: "left"}} >
                     <th style={{textAlign: "center"}}>Funding Envelope</th>
                     <th>Annual Province Base Funding</th>
                     <th>Projected Annual Parent Fees</th>
                     <th>Projected Annual Base Funding</th>
                </tr>
                <tr style={{ fontWeight: "bold"}} > 
                     <td style={{textAlign: "left", minWidth:"250px"}}><Label style={{textAlign: "left", fontWeight: "bold"}}>Instructional Human Resources</Label></td>
                     <td><TextField type="number" 
                                prefix="$"
                                value={(HRTotal_Colum04 - HRTotal_Colum03).toFixed(2).toString()}                                  
                                key={"hrtotal1"} 
                                disabled={true} />
                                </td>
                    <td><TextField type="number" 
                                prefix="$"
                                value={HRTotal_Colum03.toFixed(2).toString()}                                 
                                key={"total2"} 
                                disabled={true} />
                                </td>
                    <td><TextField type="number" 
                                prefix="$"
                                value={HRTotal_Colum04.toFixed(2).toString()}                                
                                key={"total3"} 
                                disabled={true} />
                                </td>
                 </tr>
                 {dataHR.map((val, key) => {
                    return (
                        <tr key={key}>
                            <td style={{textAlign: "left", minWidth:"250px", fontStyle:"italic"}}><Label>&#160;&#160;{val.column01}</Label></td>
                            <td><TextField type="number" 
                                prefix="$"
                                value={(val.column04.control.raw! - val.column03.control.raw!).toFixed(2).toString()}                                
                                key={val.column02.name} 
                                disabled={true} />
                            </td>
                            <td><EnvelopeField
                                field={val.column03}
                                amount={val.column03.control.raw!}
                                min={0}
                                max={val.column04.control.raw!}
                                isReadOnly={false}
                                isMasked={val.column03.control.attributes?.IsSecured||false}
                                onValueChanged={onValueChanged}
                                key={val.column03.name} 
                              ></EnvelopeField> 
                            </td>
                            <td><EnvelopeField
                                field={val.column04}
                                amount={val.column04.control.raw!}
                                min={0}
                                max={val.column04.control.attributes?.MaxValue}
                                isReadOnly={false}
                                isMasked={val.column04.control.attributes?.IsSecured||false}
                                onValueChanged={onValueChanged}
                                key={val.column04.name} 
                              ></EnvelopeField>                          
                            </td>     
                        </tr>
                    )
                })}
                  {dataNonHR.map((val, key) => {
                    return (
                            <tr key={key}>
                              <td style={{textAlign: "left", minWidth:"250px"}}><Label>{val.column01}</Label></td>
                              <td><TextField type="number" 
                                  prefix="$"
                                  value={(val.column04.control.raw! - val.column03.control.raw!).toFixed(2).toString()}                                 
                                  key={val.column02.name} 
                                  disabled={true} />
                              </td>
                              <td><EnvelopeField
                                  field={val.column03}
                                  amount={val.column03.control.raw!}
                                  min={0}
                                  max={val.column04.control.raw!}
                                  isReadOnly={false}
                                  isMasked={val.column03.control.attributes?.IsSecured||false}
                                  onValueChanged={onValueChanged}
                                  key={val.column03.name} 
                                ></EnvelopeField> 
                              </td>
                              <td><EnvelopeField
                                  field={val.column04}
                                  amount={val.column04.control.raw!}
                                  min={0}
                                  max={val.column04.control.attributes?.MaxValue}
                                  isReadOnly={false}
                                  isMasked={val.column04.control.attributes?.IsSecured||false}
                                  onValueChanged={onValueChanged}
                                  key={val.column04.name} 
                                ></EnvelopeField>                          
                              </td>
                          </tr>           
                          )
                })}
                </tbody>
                <tfoot>
                  <tr>
                    <td><Label style={{textAlign: "left", fontWeight: "bolder", fontSize:15}}>Total</Label></td>
                    <td><TextField type="number" 
                                prefix="$"
                                value={((HRTotal_Colum04 + NonHRTotal_Colum04) - (HRTotal_Colum03 + NonHRTotal_Colum03)).toFixed(2).toString()}                                 
                                key={"total1"} 
                                disabled={true} />
                    </td>
                    <td><TextField type="number" 
                                prefix="$"
                                value={(HRTotal_Colum03 + NonHRTotal_Colum03).toFixed(2).toString()}                                
                                key={"total2"} 
                                disabled={true} />
                    </td>
                    <td><TextField type="number" 
                                prefix="$"
                                value={(HRTotal_Colum04 + NonHRTotal_Colum04).toFixed(2).toString()}                               
                                key={"total3"} 
                                disabled={true} />
                    </td>
                  </tr>
                </tfoot> 
            </table>
          </Stack>
          <Stack style={{width:"100%"}} >   
            <table style={{width:"100%"}} > 
              <thead>
                <tr style={{textAlign: "left"}}>
                     <th>Monthly Base Funding</th>
                     <th>Monthly Province Base Funding</th>
                     <th>Projected Annual Parent Fees</th>
                     <th>Projected Total Monthly Base Funding</th>
                </tr>
              </thead>
              <tbody>
                <tr style={{ fontWeight: "bold"}}> 
                    <td style={{textAlign: "left", minWidth:"300px", fontSize:15}}><Label style={{textAlign: "left", fontWeight: "bold"}}>Year 1</Label></td>                    
                    <td><Label style={{textAlign: "left"}}>$ {(((HRTotal_Colum04 + NonHRTotal_Colum04) - (HRTotal_Colum03 + NonHRTotal_Colum03))/12).toFixed(2).toString()}</Label>
                    </td>
                    <td><Label style={{textAlign: "left"}}>$ {((HRTotal_Colum03 + NonHRTotal_Colum03)/12).toFixed(2).toString()}</Label>
                    </td>
                    <td><Label style={{textAlign: "left"}}>$ {((HRTotal_Colum04 + NonHRTotal_Colum04)/12).toFixed(2).toString()}</Label> 
                    </td>
                </tr>
                <tr style={{textAlign: "left"}}>
                    <td><Label style={{textAlign: "left", minWidth:"325px", fontWeight: "bolder", fontSize:15}}>Year 2 & 3</Label></td>
                    <td><Label style={{textAlign: "left"}}>$ {(((HRTotal_Colum04 + NonHRTotal_Colum04) - (HRTotal_Colum03 + NonHRTotal_Colum03))/12).toFixed(2).toString() + infText}</Label>
                    </td>
                    <td><Label style={{textAlign: "left"}}>$ {((HRTotal_Colum03 + NonHRTotal_Colum03)/12).toFixed(2).toString() + infText}</Label>
                    </td>
                    <td><Label style={{textAlign: "left"}}>$ {((HRTotal_Colum04 + NonHRTotal_Colum04)/12).toFixed(2).toString() + infText}</Label> 
                    </td>
                </tr>               
              </tbody>
            </table>
          </Stack>
        </Stack>
        );
    }, (prevProps, newProps)=> { 
        return newProps.fields.every((newField, index) => newField.control.raw === prevProps.fields[index]?.control.raw )
        && prevProps.onValueChanged === newProps.onValueChanged
        && prevProps.isReadOnly === newProps.isReadOnly
        && prevProps.isMasked === newProps.isMasked
    });