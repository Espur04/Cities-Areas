import React from "react";
import { getModule, type ModRegistrar } from "cs2/modding";
import { selectedInfo } from "cs2/bindings";
import { LocalizedEntityName, useLocalization, type Name } from "cs2/l10n";

type Entity = { index: number; version: number };
type CommuneRow = { entity: Entity; name: Name; area?: number; residents?: number; density?: number; serviceIcon?: string | null };
type CommunePanelProps = {
    group: string; area: number; residents: number; households: number; maxHouseholds: number; animals: number; density: number;
    unassignedArea: number; unassignedResidents: number; unassignedDensity: number;
    districts: CommuneRow[]; language: number; units: number; ageData: { values: number[]; total: number }; educationData: { values: number[]; total: number };
};
type DistrictAreaProps = { area: number; density: number; language: number; units: number };
type WealthProps = { group: string; language: number; householdCount: number; maxHouseholds: number; residentCount: number; petCount: number; wealthKey: string; wealthData: { income: number; rent: number; upkeep: number; resourceCost: number; fees: number } };

const InfoLink: any = getModule("game-ui/game/components/selected-info-panel/shared-components/info-link/info-link.tsx", "InfoLink");
const InfoRow: any = getModule("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx", "InfoRow");
const InfoSection: any = getModule("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx", "InfoSection");
const InfoSectionFoldout: any = getModule("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section-foldout.tsx", "InfoSectionFoldout");
const FormattedParagraphs: any = getModule("game-ui/common/text/formatted-paragraphs.tsx", "FormattedParagraphs");
const NativeTooltip: any = getModule("game-ui/common/tooltip/tooltip.tsx", "Tooltip");
const InfoSectionDivider: any = getModule("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx", "Divider");
const InfoBarChart: any = getModule("game-ui/common/charts/bar-chart/info-bar-chart.tsx", "InfoBarChart");
const AgeChart: any = getModule("game-ui/common/charts/pie-chart/age-pie-chart.tsx", "agePieChartColors");
const AgeChartLabels: any = getModule("game-ui/common/charts/pie-chart/age-pie-chart.tsx", "agePieChartLabels");
const EducationChart: any = getModule("game-ui/common/charts/pie-chart/education-pie-chart.tsx", "educationPieChartColors");
const EducationChartLabels: any = getModule("game-ui/common/charts/pie-chart/education-pie-chart.tsx", "educationPieChartLabels");
const AverageHappinessNotification: any = getModule("game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "AverageHappinessNotification");
const HappinessClasses: any = getModule("game-ui/game/components/selected-info-panel/selected-info-sections/shared-sections/notification-sections/notifications-section.module.scss", "classes");
const LocalizedNumber: any = getModule("game-ui/common/localization/localized-number.tsx", "LocalizedNumber");
const Loc: any = getModule("game-ui/common/localization/loc.generated.ts", "Loc");
const uiText = [
    ["Municipal area", "Districts", "Population", "Area", "Density", "No district", "District area", "Wealth"],
    ["Municipal area", "Districts", "Population", "Area", "Density", "No district", "District area", "Wealth"],
    ["Territoire communal", "Quartiers", "Population", "Superficie", "Densité", "Aucun quartier", "Territoire du quartier", "Richesse"],
    ["Área municipal", "Distritos", "Población", "Superficie", "Densidad", "Sin distrito", "Área del distrito", "Riqueza"],
    ["Area municipale", "Distretti", "Popolazione", "Superficie", "Densità", "Nessun distretto", "Area del distretto", "Ricchezza"],
    ["市政区域", "行政区", "人口", "面积", "人口密度", "未分配行政区", "行政区面积", "财富"],
    ["Kommunalgebiet", "Bezirke", "Bevölkerung", "Fläche", "Dichte", "Kein Bezirk", "Bezirksfläche", "Wohlstand"],
    ["自治体エリア", "地区", "人口", "面積", "人口密度", "地区なし", "地区の面積", "豊かさ"],
    ["Obszar gminny", "Dzielnice", "Ludność", "Powierzchnia", "Gęstość", "Brak dzielnicy", "Powierzchnia dzielnicy", "Zamożność"],
    ["Área municipal", "Distritos", "População", "Área", "Densidade", "Sem distrito", "Área do distrito", "Riqueza"],
    ["시 자치구역", "구역", "인구", "면적", "밀도", "미지정 구역", "구역 면적", "부"],
    ["Муниципальная территория", "Районы", "Население", "Площадь", "Плотность", "Без района", "Площадь района", "Достаток"],
    ["市政區域", "行政區", "人口", "面積", "人口密度", "未分配行政區", "行政區面積", "財富"],
];
const areaDensityHelp = [
    ["Total surface inside this commune.", "Residents per square kilometre of communal territory."],
    ["Total surface inside this commune.", "Residents per square kilometre of communal territory."],
    ["Superficie totale à l’intérieur de cette commune.", "Nombre d’habitants par kilomètre carré du territoire communal."],
    ["Superficie total dentro de esta comuna.", "Habitantes por kilómetro cuadrado del territorio municipal."],
    ["Superficie totale all’interno di questo comune.", "Abitanti per chilometro quadrato del territorio comunale."],
    ["市辖区内的土地总面积。", "市辖区每平方公里的居民人数。"],
    ["Gesamtfläche innerhalb dieser Gemeinde.", "Einwohner pro Quadratkilometer des Gemeindegebiets."],
    ["自治体内の総面積。", "自治体の1平方キロメートルあたりの住民数。"],
    ["Łączna powierzchnia tej gminy.", "Liczba mieszkańców na kilometr kwadratowy gminy."],
    ["Área total dentro desta comuna.", "Habitantes por quilómetro quadrado do território municipal."],
    ["시 자치구역의 총 면적입니다.", "시 자치구역 1제곱킬로미터당 주민 수입니다."],
    ["Общая площадь этой коммуны.", "Число жителей на квадратный километр территории коммуны."],
    ["市政區域內的總面積。", "市政區域每平方公里的居民人數。"],
];
const getLabels = (language: number) => {

return uiText[Math.min(Math.max(language || 1, 1), 12)];
};
const FoldoutHeader = ({ label, right }: { label: string; right?: React.ReactNode }) =>
    InfoRow ? <InfoRow left={label} right={right} uppercase disableFocus /> : <span style={{ textTransform: "uppercase" }}>{label}{right != null && <span style={{ float: "right" }}>{right}</span>}</span>;
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
    const localization = useLocalization();
    const t = getLabels(props.language);
    const language = Math.min(Math.max(props.language || 1, 1), 12);
    const help = areaDensityHelp[Math.min(Math.max(props.language || 1, 1), 12)];
    const { number, area, density } = getFormatters(props.units, 0, props.language);
    const sortedDistricts = [...props.districts].sort((a, b) => nameSortKey(a.name, localization).localeCompare(nameSortKey(b.name, localization), undefined, { sensitivity: "base", numeric: true }));
    return <div style={{ boxSizing: "border-box", width: "100%", minWidth: 0 }}>
        {InfoSectionFoldout ? <InfoSectionFoldout header={<FoldoutHeader label={t[0]} />} initialExpanded={true} disableFocus tooltip={FormattedParagraphs && <FormattedParagraphs>{[
            "The communal territory and its residents.", "The communal territory and its residents.", "Le territoire de la commune et ses habitants.", "El territorio municipal y sus habitantes.", "Il territorio comunale e i suoi abitanti.", "市辖区及其居民。", "Das Gemeindegebiet und seine Einwohner.", "自治体の区域と住民。", "Terytorium gminy i jej mieszkańcy.", "O território municipal e os seus habitantes.", "시 자치구역과 주민입니다.", "Территория коммуны и её жители.", "市政區域及其居民。"
        ][language]}</FormattedParagraphs>}>
        {InfoRow && <>
            <InfoSection disableFocus tooltip={<>
                <FormattedParagraphs>{[
                    "The number of residents currently living in this commune.", "The number of residents currently living in this commune.", "Nombre de personnes qui vivent actuellement dans cette commune.", "Número de personas que viven actualmente en este municipio.", "Numero di persone che vivono attualmente in questo comune.", "目前居住在本市辖区的居民人数。", "Anzahl der Einwohner, die derzeit in dieser Gemeinde leben.", "Obecna liczba mieszkańców tej gminy.", "Liczba mieszkańców obecnie żyjących w tej gminie.", "Número de habitantes que vivem atualmente nesta comuna.", "현재 시 자치구역에 거주하는 주민 수입니다.", "Число жителей, проживающих в этой коммуне.", "目前居住在此市政區域的居民人數。"
                ][language]}</FormattedParagraphs>
                {InfoSectionDivider && <InfoSectionDivider />}
                {InfoBarChart && <InfoBarChart colors={AgeChart} labels={AgeChartLabels} data={props.ageData} />}
                {InfoSectionDivider && <InfoSectionDivider />}
                <FormattedParagraphs><Loc.SelectedInfoPanel.TOOLTIP hash="ResidentsSectionAge" /></FormattedParagraphs>
                {InfoSectionDivider && <InfoSectionDivider />}
                {InfoBarChart && <InfoBarChart colors={EducationChart} labels={EducationChartLabels} data={props.educationData} />}
                {InfoSectionDivider && <InfoSectionDivider />}
                <FormattedParagraphs><Loc.SelectedInfoPanel.TOOLTIP hash="ResidentsSectionEducation" /></FormattedParagraphs>
            </>}>
                <InfoRow left={language === 2 ? "POPULATION" : ["POPULATION", "POPULATION", "POPULATION", "POBLACIÓN", "POPOLAZIONE", "人口", "BEVÖLKERUNG", "人口", "LUDNOŚĆ", "POPULAÇÃO", "인구", "НАСЕЛЕНИЕ", "人口"][language]} right={<LocalizedNumber value={props.residents} unit="integer" />} uppercase disableFocus />
                {props.animals > 0 && <InfoRow icon="Media/Game/Icons/Pet.svg" left={<LocalizedNumber value={props.animals} unit="integer" />} subRow disableFocus />}
            </InfoSection>
            <NativeTooltip direction="right" alignment="center" tooltip={FormattedParagraphs && <FormattedParagraphs>{[
                "Occupied homes compared with the total housing capacity of this commune.", "Occupied homes compared with the total housing capacity of this commune.", "Logements occupés par rapport à la capacité totale de cette commune.", "Viviendas ocupadas respecto a la capacidad total del municipio.", "Abitazioni occupate rispetto alla capacità totale del comune.", "本市辖区已入住住宅与住宅总容量的对比。", "Belegte Wohnungen im Verhältnis zur gesamten Wohnkapazität der Gemeinde.", "自治体の総住宅数に対する居住中の住宅数です。", "Zajęte mieszkania w stosunku do całkowitej pojemności mieszkaniowej gminy.", "Habitações ocupadas em relação à capacidade total desta comuna.", "시 자치구역의 총 주택 수 대비 거주 중인 주택 수입니다.", "Занятые дома относительно общей жилой вместимости коммуны.", "已入住住宅與市政區域總住宅容量的比較。"
            ][language]}</FormattedParagraphs>}>
                <InfoRow left={<Loc.SelectedInfoPanel.HOUSEHOLDS />} right={<><LocalizedNumber value={props.households} unit="integer" />{" / "}<LocalizedNumber value={props.maxHouseholds} unit="integer" /></>} uppercase disableFocus />
            </NativeTooltip>
            <InfoRow left={t[3]} right={area(props.area)} uppercase disableFocus tooltip={FormattedParagraphs && <FormattedParagraphs>{help[0]}</FormattedParagraphs>} />
            <InfoRow left={t[4]} right={density(props.density)} uppercase disableFocus tooltip={FormattedParagraphs && <FormattedParagraphs>{help[1]}</FormattedParagraphs>} />
        </>}
        </InfoSectionFoldout> : null}
        {InfoSectionFoldout ? <InfoSectionFoldout header={<FoldoutHeader label={t[1]} right={number(props.districts.length)} />} initialExpanded={false} disableFocus tooltip={FormattedParagraphs && <FormattedParagraphs>{[
            "List of districts intersecting this commune. Population, area, and density shown for each district count only the part inside this commune.", "List of districts intersecting this commune. Population, area, and density shown for each district count only the part inside this commune.", "Liste des quartiers intersectant le périmètre de cette commune. La population, la superficie et la densité indiquées pour chaque quartier ne concernent que sa partie située dans la commune.", "Lista de barrios que se cruzan con este municipio. La población, superficie y densidad de cada barrio solo cuentan la parte situada dentro del municipio.", "Elenco dei quartieri che intersecano questo comune. Popolazione, superficie e densità mostrate considerano solo la parte interna al comune.", "与本市辖区相交的行政区列表。每个行政区显示的人口、面积和密度仅统计位于本市辖区内的部分。", "Liste der Bezirke, die diese Gemeinde überschneiden. Einwohnerzahl, Fläche und Dichte beziehen sich jeweils nur auf den Teil innerhalb der Gemeinde.", "自治体の範囲と重なる地区の一覧です。各地区の人口、面積、密度は自治体内にある部分のみを集計します。", "Lista dzielnic przecinających się z tą gminą. Podane populacja, powierzchnia i gęstość obejmują tylko część leżącą w granicach gminy.", "Lista dos bairros que se cruzam com esta comuna. A população, área e densidade apresentadas incluem apenas a parte dentro da comuna.", "시 자치구역과 겹치는 구역 목록입니다. 각 구역의 인구, 면적, 밀도는 자치구역 안에 포함된 부분만 집계합니다.", "Список районов, пересекающих коммуну. Указаны население, площадь и плотность только части внутри коммуны.", "與此市政區域相交的行政區列表。每個行政區的人口、面積和密度僅統計市政區域內的部分。"
        ][language]}</FormattedParagraphs>}>
            {sortedDistricts.map((district) => <React.Fragment key={`${district.entity.index}:${district.entity.version}`}>
                <InfoRow left={<NativeLink entity={district.entity}><LocalizedEntityName value={district.name} /></NativeLink>} right={number(district.residents || 0)} disableFocus />
                <InfoRow left="" right={<>{area(district.area || 0)} · {density(district.density || 0)}</>} subRow subRowDimmed disableFocus />
            </React.Fragment>)}
            <InfoRow left={t[5]} right={number(props.unassignedResidents)} disableFocus />
            <InfoRow left="" right={<>{area(props.unassignedArea)} · {density(props.unassignedDensity)}</>} subRow subRowDimmed disableFocus />
        </InfoSectionFoldout> : null}
        <div style={{ height: "1rem", background: "rgba(255,255,255,0.2)", margin: "3rem 10rem" }} />
    </div>;
};

const nameSortKey = (name: Name, localization: ReturnType<typeof useLocalization>) => {
    if ("name" in name) return name.name;
    return localization.translate(name.nameId) || name.nameId;
};

const CommuneWealthSection = (props: WealthProps) => {
    const money = (value: number) => <LocalizedNumber value={value / Math.max(props.householdCount, 1)} unit="moneyPerMonth" />;
    const content = InfoSectionFoldout ? <InfoSectionFoldout header={<FoldoutHeader label={getLabels(props.language)[7]} />} initialExpanded={false} disableFocus>
        {InfoRow && <>
            <InfoRow left={<Loc.SelectedInfoPanel.AVERAGE_HOUSEHOLD_WEALTH />} right={<Loc.SelectedInfoPanel.CITIZEN_WEALTH hash={props.wealthKey} />} disableFocus />
            <InfoRow left={<Loc.SelectedInfoPanel.AVERAGE_HOUSEHOLD_INCOME />} right={money(props.wealthData.income)} disableFocus />
            <InfoRow left={<Loc.SelectedInfoPanel.AVERAGE_HOUSEHOLD_RENT />} right={money(props.wealthData.rent)} disableFocus />
            <InfoRow left={<Loc.SelectedInfoPanel.AVERAGE_HOUSEHOLD_UPKEEP />} right={money(props.wealthData.upkeep)} disableFocus />
            <InfoRow left={<Loc.SelectedInfoPanel.AVERAGE_HOUSEHOLD_RESOURCE_COST />} right={money(props.wealthData.resourceCost)} disableFocus />
            <InfoRow left={<Loc.SelectedInfoPanel.AVERAGE_HOUSEHOLD_FEES />} right={money(props.wealthData.fees)} disableFocus />
        </>}
    </InfoSectionFoldout> : null;
    const wealthHelp = ["Average household finances in this commune, including household income and costs for rent, building upkeep, resources, and service fees.", "Average household finances in this commune, including household income and costs for rent, building upkeep, resources, and service fees.", "La situation financière moyenne des ménages dans cette commune, en tenant compte des revenus de leurs membres et des coûts liés au loyer, à l'entretien du bâtiment, aux ressources et aux frais de service.", "La situación financiera media de los hogares de este municipio, incluidos sus ingresos y los costes del alquiler, mantenimiento del edificio, recursos y servicios.", "La situazione finanziaria media delle famiglie di questo comune, considerando redditi e costi di affitto, manutenzione, risorse e servizi.", "本市辖区家庭的平均财务状况，包括收入以及租金、建筑维护、资源和服务费用。", "Die durchschnittliche finanzielle Lage der Haushalte in dieser Gemeinde, einschließlich Einkommen sowie Miet-, Gebäudeunterhalts-, Ressourcen- und Servicekosten.", "自治体の世帯の平均的な家計状況です。収入のほか、家賃、建物の維持費、資源、サービス料を含みます。", "Średnia sytuacja finansowa gospodarstw domowych w tej gminie, obejmująca dochody oraz koszty czynszu, utrzymania budynków, zasobów i usług.", "A situação financeira média dos agregados desta comuna, incluindo rendimentos e custos de renda, manutenção dos edifícios, recursos e serviços.", "시 자치구역 가구의 평균 재정 상태입니다. 소득과 임대료, 건물 유지비, 자원 및 서비스 요금을 포함합니다.", "Среднее финансовое положение домохозяйств этой коммуны: доходы, аренда, содержание зданий, ресурсы и плата за услуги.", "此市政區域家庭的平均財務狀況，包括收入，以及租金、建築維護、資源和服務費用。"][Math.min(Math.max(props.language || 1, 1), 12)];
    return NativeTooltip && FormattedParagraphs ? <NativeTooltip direction="right" alignment="center" tooltip={<FormattedParagraphs>{wealthHelp}</FormattedParagraphs>}>{content}</NativeTooltip> : content;
};

const CommuneHappinessSection = (props: any) => {
    const happinessHelp = [
        "The average happiness of households living in this commune. Happiness combines citizen health and well-being. Meeting basic needs, healthcare, education, leisure, and safety affects these values. Happy citizens work more effectively and improve company efficiency.",
        "The average happiness of households living in this commune. Happiness combines citizen health and well-being. Meeting basic needs, healthcare, education, leisure, and safety affects these values. Happy citizens work more effectively and improve company efficiency.",
        "Le bonheur moyen des ménages vivant dans cette commune. Le bonheur des citoyens combine leur santé et leur bien-être. La prise en charge de leurs besoins fondamentaux, notamment les soins de santé, améliore leur santé. L’éducation, les loisirs et la sécurité accroissent leur bien-être. Les citoyens heureux travaillent plus efficacement et augmentent aussi l’efficacité des entreprises.",
        "La felicidad media de los hogares de este municipio. Combina la salud y el bienestar de sus habitantes. La atención a sus necesidades, la sanidad, la educación, el ocio y la seguridad influyen en estos valores. Los ciudadanos felices trabajan mejor y aumentan la eficiencia de las empresas.",
        "La felicità media delle famiglie di questo comune. Combina salute e benessere dei cittadini. Bisogni essenziali, assistenza sanitaria, istruzione, svago e sicurezza influenzano questi valori. I cittadini felici lavorano meglio e aumentano l'efficienza delle aziende.",
        "本市辖区家庭的平均幸福感。幸福感由居民的健康和生活质量构成。基本需求、医疗、教育、休闲和安全都会影响这些数值。幸福的居民工作效率更高，也会提升公司的效率。",
        "Die durchschnittliche Zufriedenheit der Haushalte in dieser Gemeinde. Sie setzt sich aus Gesundheit und Wohlbefinden zusammen. Grundbedürfnisse, Gesundheitsversorgung, Bildung, Freizeit und Sicherheit beeinflussen diese Werte. Zufriedene Einwohner arbeiten effizienter und steigern die Effizienz der Unternehmen.",
        "自治体に住む世帯の平均的な幸福度です。市民の健康と幸福感で決まります。基本的な需要、医療、教育、余暇、安全がこれらの値に影響します。幸福な市民は効率よく働き、企業の効率も高めます。",
        "Średnie zadowolenie gospodarstw domowych w tej gminie. Łączy zdrowie i samopoczucie mieszkańców. Podstawowe potrzeby, opieka zdrowotna, edukacja, rozrywka i bezpieczeństwo wpływają na te wartości. Szczęśliwi mieszkańcy pracują wydajniej i zwiększają wydajność firm.",
        "A felicidade média dos agregados desta comuna. Combina saúde e bem-estar. As necessidades básicas, os cuidados de saúde, a educação, o lazer e a segurança influenciam estes valores. Cidadãos felizes trabalham melhor e aumentam a eficiência das empresas.",
        "시 자치구역 가구의 평균 행복도입니다. 주민의 건강과 삶의 질로 결정됩니다. 기본 욕구, 의료, 교육, 여가, 안전이 이 값에 영향을 줍니다. 행복한 주민은 더 효율적으로 일해 기업의 효율도 높입니다.",
        "Среднее счастье домохозяйств этой коммуны. Оно складывается из здоровья и благополучия жителей. Основные потребности, здравоохранение, образование, досуг и безопасность влияют на эти показатели. Счастливые жители работают эффективнее и повышают эффективность компаний.",
        "此市政區域家庭的平均幸福感，由居民的健康與生活品質構成。基本需求、醫療、教育、休閒和安全都會影響這些數值。幸福的居民工作效率較高，也能提升公司的效率。"
    ][Math.min(Math.max(props.language || 1, 1), 12)];
    const factors = props.happinessFactors || [];
    if (!NativeTooltip || !AverageHappinessNotification) return null;
    return <NativeTooltip direction="right" alignment="center" tooltip={<>
        {factors.length > 0 && <>
            <div className={HappinessClasses?.factors}>
                {factors.map((factor: any, index: number) => <InfoRow key={index}
                    left={factor.weight > 0 ? <Loc.Common.HAPPINESS_FACTOR_POSITIVE hash={factor.factor} /> : <Loc.Common.HAPPINESS_FACTOR_NEGATIVE hash={factor.factor} />}
                    right={<span style={{ color: factor.weight > 0 ? "#77e83f" : "#ff755f" }}>{factor.weight > 0 ? "+" : ""}{factor.weight}</span>} disableFocus />)}
            </div>
            {InfoSectionDivider && <InfoSectionDivider />}
        </>}
        <FormattedParagraphs>{happinessHelp}</FormattedParagraphs>
    </>}>
        <div className={HappinessClasses?.notificationsSection}>
            <AverageHappinessNotification notification={props.averageHappiness} />
        </div>
    </NativeTooltip>;
};

const DistrictAreaSection = (props: DistrictAreaProps) => {
    const t = getLabels(props.language);
    const { area, density } = getFormatters(props.units, 0, props.language);
    if (!InfoSection || !InfoRow) return null;
    return <InfoSection disableFocus>
        <InfoRow left={t[6]} uppercase disableFocus />
        <InfoRow left={t[3]} right={area(props.area || 0)} disableFocus />
        <InfoRow left={t[4]} right={density(props.density || 0)} disableFocus />
    </InfoSection>;
};

const register: ModRegistrar = (moduleRegistry) => {
    const path = "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
    const components = moduleRegistry.get(path, "selectedInfoSectionComponents");
    moduleRegistry.override(path, "selectedInfoSectionComponents", { ...components,
        "Communes.CommunePanelSection": CommunePanelSection,
        "Communes.CommuneWealthSection": CommuneWealthSection,
        "Communes.DistrictAreaSection": DistrictAreaSection,
        "Communes.CommuneHappinessSection": CommuneHappinessSection,
    });
};

export default register;
