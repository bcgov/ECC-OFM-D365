import * as React from 'react';
import { ReactNode, CSSProperties } from 'react';
import { DelayedRender, Callout, Text, mergeStyleSets } from '@fluentui/react';
import { useBoolean, useId } from '@fluentui/react-hooks';
import { error } from 'console';
interface RadialControlProps {
  children: ReactNode;
  tooltip: string;
  size: string | number;
  color: string;

}
interface RadialControlSpecialProps {
  children: ReactNode;
  tooltip: string;
  size: string | number;
  color: string;
  parentId: string | undefined;
  webAPI: ComponentFramework.WebApi;
  peopleCache: { [key: string]: any }

}

export const RadialControlSpecial = React.memo(function PeopleRaw({ children, tooltip, size, color, parentId, webAPI, peopleCache }: RadialControlSpecialProps) {
  const [people, setPeople] = React.useState<Array<any> | null>(peopleCache[parentId ?? ""] ?? null);
  const mounted = React.useRef(false);

  React.useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      //  console.log(`People component unmounted for ${parentId}`);
    };
  }, []);

  React.useEffect(() => {
    if (parentId && peopleCache[parentId ?? ""] == null) {
      console.log(`%cStarting fetch for ${parentId}`, "color:yellow");
      webAPI.retrieveMultipleRecords("ofm_application", ["?fetchXml=",
        "<fetch top='50'>",
        "  <entity name='ofm_application'>",
        "    <attribute name='ofm_application_score_errors'/>",
        "    <filter>",
        "      <condition attribute='ofm_applicationid' operator='eq' value='", parentId/*00000000-0000-0000-0000-000000000000*/, "'/>",
        "    </filter>",
        "  </entity>",
        "</fetch>"
      ].join("")).then((result) => {
        peopleCache[parentId ?? ""] = result.entities;
        if (mounted.current) {
          setPeople(result.entities);
        }
        /*  else {
              console.log(`%cPeople component unmounted before data returned for ${parentId}`, "color:red");
          }*/
      }).catch((error)=> { console.error(error) });
    }
    /*  else {
          console.log(`%cUsing cache data for ${parentId}`, "color:green");
      }  */
  }, [parentId]);

  if (people != null && people.length > 0) {
    //@ts-ignore
    if (people[0].ofm_application_score_errors > 0) {

      color = "#a30805";

    }


  }

  const styles = mergeStyleSets({
    callout: {
      width: 320,
      maxWidth: '90%',
      padding: '20px 24px',
    },
    CSSProperties: {
      margin: "0 auto",
      width: size,
      height: size,
      borderRadius: '50%',
      backgroundColor: color,
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
    }
  });



  return (<><div className={styles.CSSProperties}
  >{children} </div>
  </>);
});




export const RadialControl: React.FC<RadialControlProps> = ({ children, tooltip, size, color }) => {

  const styles = mergeStyleSets({
    callout: {
      width: 320,
      maxWidth: '90%',
      padding: '20px 24px',
    },
    CSSProperties: {
      margin: "0 auto",
      width: size,
      height: size,
      borderRadius: '50%',
      backgroundColor: color,
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
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

