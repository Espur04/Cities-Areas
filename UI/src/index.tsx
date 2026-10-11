import React from "react";
import { getModule, type ModRegistrar } from "cs2/modding";
import { selectedInfo } from "cs2/bindings";
import { LocalizedEntityName, type Name } from "cs2/l10n";

type Entity = { index: number; version: number };
type CommuneRow = { entity: Entity; name: Name; area?: number; residents?: number; density?: number; serviceIcon?: string | null };
type CommunePanelProps = {
    group: string; area: number; residents: number; density: number;
    unassignedArea: number; unassignedResidents: number; unassignedDensity: number;
    districts: CommuneRow[]; language: number; units: number;
};
type DistrictAreaProps = { area: number; density: number; language: number; units: number };

const InfoLink: any = getModule("game-ui/game/components/selected-info-panel/shared-components/info-link/info-link.tsx", "InfoLink");
const uiText = [
    ["Municipal area", "Districts", "Population", "Area", "Density", "No district", "District area"],
    ["Municipal area", "Districts", "Population", "Area", "Density", "No district", "District area"],
    ["Territoire communal", "Quartiers", "Population", "Superficie", "Densité", "Aucun quartier", "Territoire du quartier"],
    ["Área municipal", "Distritos", "Población", "Superficie", "Densidad", "Sin distrito", "Área del distrito"],
    ["Area municipale", "Distretti", "Popolazione", "Superficie", "Densità", "Nessun distretto", "Area del distretto"],
    ["市政区域", "行政区", "人口", "面积", "人口密度", "未分配行政区", "行政区面积"],
    ["Kommunalgebiet", "Bezirke", "Bevölkerung", "Fläche", "Dichte", "Kein Bezirk", "Bezirksfläche"],
    ["自治体エリア", "地区", "人口", "面積", "人口密度", "地区なし", "地区の面積"],
    ["Obszar gminny", "Dzielnice", "Ludność", "Powierzchnia", "Gęstość", "Brak dzielnicy", "Powierzchnia dzielnicy"],
    ["Área municipal", "Distritos", "População", "Área", "Densidade", "Sem distrito", "Área do distrito"],
    ["시 자치구역", "구역", "인구", "면적", "밀도", "미지정 구역", "구역 면적"],
    ["Муниципальная территория", "Районы", "Население", "Площадь", "Плотность", "Без района", "Площадь района"],
    ["市政區域", "行政區", "人口", "面積", "人口密度", "未分配行政區", "行政區面積"],
];
const uiIds = ["Territory", "Districts", "Population", "Area", "Density", "NoDistrict", "DistrictTerritory"];
const getLabels = (language: number) => {

return uiText[Math.min(Math.max(language || 1, 1), 12)];
};
const getFormatters = (units: number, gameUnitSystem: number, language: number) => {
    const imperial = units === 2 || (units === 0 && gameUnitSystem === 1);
    const commaDecimal = [2, 3, 4, 6, 8, 9].includes(language);
    const number = (value: number) => Math.round(value || 0).toString().replace(/\B(?=(\d{3})+(?!\d))/g, commaDecimal ? " " : ",");
    const area = (value: number) => {
        if (imperial) return value < 258_998.811 ? `${number(value * 10.7639104)} ft²` : `${(value / 2_589_988.11).toFixed(2).replace(".", commaDecimal ? "," : ".")} mi²`;
        return value < 10_000 ? `${number(value)} m²` : `${(value / 1_000_000).toFixed(2).replace(".", commaDecimal ? "," : ".")} km²`;
    };
    const density = (value: number) => `${number(imperial ? value / 0.386102 : value)} ${imperial ? "hab./mi²" : "hab./km²"}`;
    return { number, area, density };
};

const NativeLink = ({ entity, children }: { entity: Entity; children: React.ReactNode }) => {
    const [hovered, setHovered] = React.useState(false);
    const name = <span style={{ color: hovered ? "#b8e9ff" : undefined, textShadow: hovered ? "0 0 5px #54cfff99" : undefined, transition: "color 100ms ease, text-shadow 100ms ease" }}>{children}</span>;
    return <span style={{ display: "inline-block", maxWidth: "100%" }} onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}>
        {InfoLink ? React.createElement(InfoLink, { onSelect: () => selectedInfo.selectEntity(entity) }, name)
            : <button style={{ color: hovered ? "#b8e9ff" : "#65bfff", background: "transparent", border: 0, padding: 0, cursor: "pointer" }} onClick={() => selectedInfo.selectEntity(entity)}>⌕ {name}</button>}
    </span>;
};

const CommunePanelSection = (props: CommunePanelProps) => {
    const t = getLabels(props.language);
    const { number, area, density } = getFormatters(props.units, 0, props.language);
    const headerStyle: React.CSSProperties = { fontWeight: 700, fontSize: 18, marginBottom: 7, textTransform: "uppercase" };
    const statRowStyle: React.CSSProperties = { display: "flex", flexDirection: "row", justifyContent: "space-between", alignItems: "baseline", gap: 12, padding: "3px 0", fontSize: 17, minWidth: 0, width: "100%", boxSizing: "border-box" };
    const districtStyle: React.CSSProperties = { padding: "6px 0 3px" };
    const detailStyle: React.CSSProperties = { display: "flex", flexDirection: "row", justifyContent: "flex-end", alignItems: "baseline", color: "#b6b6b6", fontSize: 13, textAlign: "right", marginTop: 2, whiteSpace: "nowrap", flexWrap: "nowrap", width: "100%", minWidth: 0 };
    const valueStyle: React.CSSProperties = { flexShrink: 0, textAlign: "right", whiteSpace: "nowrap" };

    return <div style={{ boxSizing: "border-box", width: "100%", minWidth: 0, padding: "10px 36px 10px 18px", color: "#f2f2f2", fontSize: 17 }}>
        <div style={{ borderTop: "1px solid #ffffff55", borderBottom: "1px solid #ffffff55", padding: "8px 0" }}>
            <div style={headerStyle}>{t[0]}</div>
            <div style={statRowStyle}><span>{t[2]}</span><span style={valueStyle}>{number(props.residents)}</span></div>
            <div style={statRowStyle}><span>{t[3]}</span><span style={valueStyle}>{area(props.area)}</span></div>
            <div style={statRowStyle}><span>{t[4]}</span><span style={valueStyle}>{density(props.density)}</span></div>
        </div>
        <div style={{ ...headerStyle, margin: "14px 0 5px" }}>{t[1]}</div>
        {props.districts.map((district) => <div key={`${district.entity.index}:${district.entity.version}`} style={districtStyle}>
            <div style={statRowStyle}>
                <div style={{ minWidth: 0, flex: "1 1 auto", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    <NativeLink entity={district.entity}><LocalizedEntityName value={district.name} /></NativeLink>
                </div>
                <span style={valueStyle}>{number(district.residents || 0)}</span>
            </div>
            <div style={detailStyle}><span style={{ whiteSpace: "nowrap" }}>{area(district.area || 0)} · {density(district.density || 0)}</span></div>
        </div>)}
        <div style={{ ...districtStyle, paddingTop: 10 }}>
            <div style={statRowStyle}><span>{t[5]}</span><span style={valueStyle}>{number(props.unassignedResidents)}</span></div>
            <div style={detailStyle}>{area(props.unassignedArea)} · {density(props.unassignedDensity)}</div>
        </div>
    </div>;
};

const DistrictAreaSection = (props: DistrictAreaProps) => {
    const t = getLabels(props.language);
    const { number, area, density } = getFormatters(props.units, 0, props.language);
    const rowStyle: React.CSSProperties = { display: "flex", flexDirection: "row", justifyContent: "space-between", alignItems: "baseline", gap: 12, width: "100%", boxSizing: "border-box", padding: "3px 0", fontSize: 17 };
    return <div style={{ boxSizing: "border-box", width: "100%", padding: "10px 18px", color: "#f2f2f2", fontSize: 17 }}>
        <div style={{ borderTop: "1px solid #ffffff55", borderBottom: "1px solid #ffffff55", padding: "8px 0" }}>
            <div style={{ fontWeight: 700, fontSize: 18, marginBottom: 7, textTransform: "uppercase" }}>{t[6]}</div>
            <div style={rowStyle}><span>{t[3]}</span><span style={{ flexShrink: 0, textAlign: "right", whiteSpace: "nowrap" }}>{area(props.area || 0)}</span></div>
            <div style={rowStyle}><span>{t[4]}</span><span style={{ flexShrink: 0, textAlign: "right", whiteSpace: "nowrap" }}>{density(props.density || 0)}</span></div>
        </div>
    </div>;
};

const register: ModRegistrar = (moduleRegistry) => {
    const path = "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
    const components = moduleRegistry.get(path, "selectedInfoSectionComponents");
    moduleRegistry.override(path, "selectedInfoSectionComponents", { ...components,
        "Communes.CommunePanelSection": CommunePanelSection,
        "Communes.DistrictAreaSection": DistrictAreaSection,
    });
};

export default register;
