import {Filter} from "./filter";

export class FilterConnector {
    public logic: string;
    public filter: Filter

    public constructor(logic: string, filter: Filter) {
        this.logic = logic;
        this.filter = filter;
    }

    public serialize(): string {
        return `${this.logic}+${this.filter.serialize()}`;
    }

    public static Deserialize(serialized: string): FilterConnector {
        let separator = "+";
        let logicEnd = serialized.indexOf(separator);
        if ((logicEnd < 0)) {
            separator = "'";
            logicEnd = serialized.indexOf(separator);
        }

        let filterStart = (logicEnd + 1);
        let filterEnd = serialized.lastIndexOf(separator);
        let logic = serialized.substring(0, logicEnd);
        let filter = serialized.substring(filterStart);
        let fc = new FilterConnector(logic, Filter.deserialize(filter));
        return fc;
    }
}
