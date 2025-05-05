import * as React from 'react';
import { ReactNode, CSSProperties } from 'react';
import { DelayedRender, Callout, Text, mergeStyleSets } from '@fluentui/react';
import { useBoolean, useId } from '@fluentui/react-hooks';
interface RadialControlProps {
  children: ReactNode;
  tooltip: string;
  size: string | number;
  color: string;
}

const RadialControl: React.FC<RadialControlProps> = ({ children, tooltip, size, color }) => {

  const styles = mergeStyleSets({
    callout: {
      width: 320,
      maxWidth: '90%',
      padding: '20px 24px',
    },
    CSSProperties: {
      width: size,
      height: size,
      borderRadius: '50%',
      backgroundColor: color,
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
    }
  });
  
  const style = mergeStyleSets({
    CSSProperties: {
      width: size,
      height: size,
      borderRadius: '50%',
      backgroundColor: color,
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
    },
    callout: {
      width: 320,
      padding: '20px 24px',
      border: "1px solid red"
    }
  });
  const [isCalloutVisible, { toggle: toggleIsCalloutVisible }] = useBoolean(false);
  const buttonId = useId('callout-button');

  return (<><div className={styles.CSSProperties} id={buttonId}
    onMouseOver={toggleIsCalloutVisible}
    onMouseOut={toggleIsCalloutVisible}
  >{children} </div>
    {isCalloutVisible && tooltip != "" && tooltip != "null" && (
      <Callout className={styles.callout} target={`#${buttonId}`} onDismiss={toggleIsCalloutVisible} role="alert">
        <DelayedRender>
          <Text variant="large">
            {tooltip}
          </Text>
        </DelayedRender>
      </Callout>
    )}</>);
};

export default RadialControl;
