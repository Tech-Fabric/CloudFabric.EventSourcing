import {Filter} from "../../src";
import {FilterOperator} from "../../src/queries/FilterOperator";

describe('Filter serialization', () => {
    it('Should correctly serialize and deserialize filter object', () => {
        let filter = new Filter('userId', FilterOperator.Equal, 1);
        filter.tag = 'basic test filter';
        filter.visible = false;
        
        let serializedString = filter.serialize();

        expect(serializedString).toEqual('userId|eq|1|F|basic%20test%20filter|');
        
        let filterDeserialized = Filter.deserialize(serializedString);
        
        expect(filterDeserialized.propertyName).toEqual(filter.propertyName);
        expect(filterDeserialized.operator).toEqual(filter.operator);
        expect(filterDeserialized.value).toEqual(filter.value);
        expect(filterDeserialized.visible).toEqual(filter.visible);
        expect(filterDeserialized.tag).toEqual(filter.tag);
    });

    it('Should correctly serialize and deserialize filter object with nested filters', () => {
        let filter = new Filter('userId', FilterOperator.Equal, 1);
        filter.tag = 'basic test filter';
        filter.visible = false;
        
        filter.or(new Filter("age", FilterOperator.GreaterOrEqual, 18).and(new Filter('age', FilterOperator.LowerOrEqual, 25)));

        let serializedString = filter.serialize();

        expect(serializedString).toEqual('userId|eq|1|F|basic%20test%20filter|or+age|ge|18|F||and+age|le|25|F||');

        let filterDeserialized = Filter.deserialize(serializedString);

        expect(filterDeserialized.propertyName).toEqual(filter.propertyName);
        expect(filterDeserialized.operator).toEqual(filter.operator);
        expect(filterDeserialized.value).toEqual(filter.value);
        expect(filterDeserialized.visible).toEqual(filter.visible);
        expect(filterDeserialized.tag).toEqual(filter.tag);
        
        expect(serializedString).toEqual(filterDeserialized.serialize());
    });
});