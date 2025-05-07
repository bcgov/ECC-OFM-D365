import * as React from "react";
import { initializeIcons } from "@fluentui/react/lib/Icons";
import { IStackStyles } from "@fluentui/react";
import {
    GaugeChart, IGaugeChartProps, IGaugeChartStyles,
    getGradientFromToken, DataVizGradientPalette, GaugeChartVariant
} from "@fluentui/react-charting";

initializeIcons();
// React component for the gauge chart
interface GaugeChartProps {
    score: number | null;
    maxScore: number;
    highSegmentPercent: number;
    lowSegmentPercent: number;
    mediumSegmentPercent: number;
    highSegmentLabel: string;
    lowSegmentLabel: string;
    mediumSegmentLabel: string;
}

export const GaugeChartComponent: React.FC<GaugeChartProps> = ({ score, maxScore, highSegmentPercent, lowSegmentPercent, mediumSegmentPercent, highSegmentLabel, lowSegmentLabel, mediumSegmentLabel }) => {
    // Fluent UI styles
    const stackStyles: IStackStyles = {
        root: {
            width: "100%",
            height: "100%",
            alignItems: "center",
        },
    };
    const gaugeStyles: Partial<IGaugeChartStyles> = {
        chartValue: {
            fontSize: "24px", // Updated font size of the chart value
            fontWeight: 600,
            fill: "#323130",
        },
    };
    // Gauge chart configuration
    const gaugeProps: IGaugeChartProps = {
        chartValue: score !== null ? score : 0,
        styles: gaugeStyles,
        maxValue: maxScore,
        segments: [
            { size: maxScore * (lowSegmentPercent / 100), gradient: getGradientFromToken(DataVizGradientPalette.error), color: "#d13438", legend: lowSegmentLabel },
            { size: maxScore * (mediumSegmentPercent / 100), gradient: getGradientFromToken(DataVizGradientPalette.warning), color: "#d29200", legend: mediumSegmentLabel },
            { size: maxScore * (highSegmentPercent / 100), gradient: getGradientFromToken(DataVizGradientPalette.success), color: "#107c10", legend: highSegmentLabel },
        ],
        width: 300,
        height: 150,
        hideMinMax: score == null || score == 0 ? true : false,
        hideLegend: true,
        variant: GaugeChartVariant.MultipleSegments,
        chartValueFormat: ((sweepFraction: [number, number]) => {
            return sweepFraction[0] == 0 || sweepFraction[1] == 0 ? "N/A" : `${sweepFraction[0]} / ${sweepFraction[1]}`;

        })
    };

    return (
        <GaugeChart {...gaugeProps} />
    );
};